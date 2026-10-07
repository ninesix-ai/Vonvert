// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace Vonvert.Tests.UIAutomation;

/// <summary>
/// End-to-end coverage for the optional uninstall data purge. Installs a freshly
/// packaged Vonvert_Setup.exe to a temp dir, then drives the GUI uninstaller and
/// answers the purge prompt:
///   * No  branch -> user data is kept
///   * Yes branch -> the sandbox is purged
///
/// The purge is scoped to a throwaway sandbox via VONVERT_PURGE_ROOT, which the
/// installer forwards to "Vonvert.exe --purge-user-data --purge-root <sandbox>". The
/// real default data root is therefore never in scope, and each branch asserts it
/// survives. Control ids were confirmed by an in-test UIA dump: wizard Uninstall=1 /
/// Cancel=2; purge MB_YESNO Yes=6 / No=7 — all reachable via UIA AutomationId.
///
/// Prerequisites (NOT part of headless CI; run locally):
///   1. Installer built:  python build.py --package
///   2. An interactive desktop session (FlaUI drives real #32770 dialogs).
/// </summary>
[Collection("UIAutomation")]
[Trait("Category", "UIAutomation")]
public sealed class UninstallPurgeE2ETests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

    // Locale-independent Win32/UIA control ids (verified by an in-test UIA dump).
    private const string WizardPrimaryId = "1";
    private const string MsgYesId = "6";
    private const string MsgNoId = "7";

    // NSIS uses the #32770 dialog class for both the wizard and the purge prompt, so
    // the prompt is additionally matched on its own message text. The Chinese entries
    // are the localized uninstaller prompt (installer source: installer/setup.oss.nsi);
    // they must stay on the declaration line to remain inside the release scan's
    // allowlist for bilingual test data.
    private static readonly string[] PurgePromptMarkers = { "Also delete", "user data and settings", "是否同时删除", "用户数据" };

    private readonly UIA3Automation _auto = new();
    private readonly string _installDir;
    private readonly string _sandbox;      // VONVERT_PURGE_ROOT: the only dir a Yes purge may erase
    private readonly string _markerFile;

    public UninstallPurgeE2ETests()
    {
        var guid = Guid.NewGuid().ToString("N");
        _installDir = Path.Combine(Path.GetTempPath(), "vonvert-uninst-" + guid);
        _sandbox = Path.Combine(Path.GetTempPath(), "vonvert-purge-sandbox-" + guid);
        _markerFile = Path.Combine(_sandbox, "marker.json");
    }

    public void Dispose()
    {
        KillStrayUninstallers();
        // An NSIS uninstaller re-extracts itself under %TEMP%\ns*.tmp and runs as the
        // process name "Un", so a killed or failed run leaves a modal dialog holding
        // the (locked) install tree. Close those before deleting anything.
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = SafePath(p);
                if (path == null) continue;
                if (path.StartsWith(_installDir, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(Path.Combine(Path.GetTempPath(), "ns"), StringComparison.OrdinalIgnoreCase))
                {
                    p.Kill();
                    p.WaitForExit(5000);
                }
            }
            catch { /* already gone or not ours */ }
            finally { p.Dispose(); }
        }

        try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { /* best-effort */ }
        try { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); } catch { /* best-effort */ }
        _auto.Dispose();
    }

    private static string? SafePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch { return null; }   // exited, 32/64-bit mismatch or access denied
    }

    // NSIS re-extracts the uninstaller as a process literally named "Un" whose
    // MainModule is often unreadable (32/64-bit or access denied), so the path-based
    // sweep above can miss it; kill by name too.
    private static void KillStrayUninstallers()
    {
        foreach (var p in Process.GetProcessesByName("Un"))
        {
            try { p.Kill(); p.WaitForExit(5000); } catch { } finally { p.Dispose(); }
        }
    }

    // A previous test in the same UIAutomation collection can leave a modal #32770 on
    // the desktop; starting while it is still up makes WaitForWindow match that stale
    // window instead of ours. Clear the deck before each run.
    private void EnsureCleanDesktop()
    {
        KillStrayUninstallers();
        Thread.Sleep(400);
    }

    [Fact(DisplayName = "E2E-UNINST-NO: answer No to the purge prompt keeps user data")]
    public void Uninstall_NoChoice_KeepsUserData()
    {
        RunUninstall(clickYes: false, expectPurged: false);
    }

    [Fact(DisplayName = "E2E-UNINST-YES: answer Yes purges the sandbox and never touches the default root")]
    public void Uninstall_YesChoice_PurgesSandboxOnly()
    {
        RunUninstall(clickYes: true, expectPurged: true);
    }

    private void RunUninstall(bool clickYes, bool expectPurged)
    {
        // Clear any leftover uninstaller window from a sibling test before we start,
        // so WaitForWindow can only ever see the dialogs our own uninstaller raises.
        EnsureCleanDesktop();

        var setup = FindSetupPath();
        InstallSilently(setup, _installDir);

        // Seed a recognizable sandbox marker so presence/absence is unambiguous.
        Directory.CreateDirectory(_sandbox);
        File.WriteAllText(_markerFile, "{}");

        // The real default data root must survive EITHER branch; record whether it
        // existed so we can prove --purge-root actually scoped the erase.
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ninesix-ai", "Vonvert");
        var defaultExistedBefore = Directory.Exists(defaultRoot);

        try
        {
            var uninstaller = Path.Combine(_installDir, "uninstall.exe");
            Assert.True(File.Exists(uninstaller), "uninstaller not produced by install");

            var psi = new ProcessStartInfo(uninstaller) { UseShellExecute = false };
            psi.Environment["VONVERT_PURGE_ROOT"] = _sandbox;   // forwarded to --purge-root by the installer
            using var proc = Process.Start(psi)!;

            DriveUninstallWizard(clickYes);

            // An NSIS uninstaller copies itself to a temp folder, so the process we
            // started can exit while the real work continues. Wait on the observable
            // outcome instead of trusting that handle.
            if (expectPurged)
            {
                bool gone = WaitUntil(() => !File.Exists(_markerFile), TimeSpan.FromSeconds(60));
                Assert.True(gone,
                    $"Yes branch: purge prompt was answered but the sandbox marker is still present ({_markerFile}); " +
                    "check that the uninstaller forwards VONVERT_PURGE_ROOT as --purge-root and exits 0");
                if (defaultExistedBefore)
                    Assert.True(Directory.Exists(defaultRoot),
                        "Yes branch purged the REAL default data root — VONVERT_PURGE_ROOT did not scope the purge!");
            }
            else
            {
                WaitUntil(() => proc.HasExited, TimeSpan.FromSeconds(30));
                Assert.True(File.Exists(_markerFile), "No branch: expected data kept but sandbox marker is gone");
                Assert.Equal(defaultExistedBefore, Directory.Exists(defaultRoot));
            }
        }
        finally
        {
            // The purge may already have removed the sandbox; ensure it is gone.
            try { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Start the removal from the NSIS confirm wizard (primary button id 1), then answer
    /// the purge prompt (Yes id 6 / No id 7). Both are #32770 dialogs, so the prompt is
    /// matched on its own message text as well as its Yes button. If the prompt never
    /// shows, the uninstaller stopped asking and that is a product regression.
    /// </summary>
    private void DriveUninstallWizard(bool clickYes)
    {
        var wizard = WaitForWindow(w => IsDialog(w) && HasControl(w, WizardPrimaryId), Timeout);
        Assert.NotNull(wizard);
        Assert.True(InvokeById(wizard!, WizardPrimaryId),
            $"uninstall confirmation exposes no primary button (id {WizardPrimaryId}); removal cannot be started");

        var box = WaitForWindow(w => IsDialog(w)
                                     && HasControl(w, MsgYesId)
                                     && DialogTextMatches(w, PurgePromptMarkers),
                                Timeout);
        Assert.NotNull(box);
        Assert.True(InvokeById(box!, clickYes ? MsgYesId : MsgNoId),
            $"purge prompt has no {(clickYes ? "Yes" : "No")} button (id {(clickYes ? MsgYesId : MsgNoId)})");
    }

    /// <summary>Poll the desktop's top-level windows until one satisfies <paramref name="match"/>.</summary>
    private Window? WaitForWindow(Func<AutomationElement, bool> match, TimeSpan wait)
    {
        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                foreach (var w in _auto.GetDesktop().FindAllChildren())
                {
                    try { if (match(w)) return w.AsWindow(); }
                    catch { /* element went away mid-scan */ }
                }
            }
            catch { /* tree not ready */ }
            Thread.Sleep(200);
        }
        return null;
    }

    private static bool IsDialog(AutomationElement el)
    {
        try { return el.ClassName == "#32770"; }
        catch { return false; }
    }

    private static bool HasControl(AutomationElement el, string automationId)
    {
        try { return el.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null; }
        catch { return false; }
    }

    private static bool DialogTextMatches(AutomationElement el, string[] markers)
    {
        try
        {
            if (el.Name != null && markers.Any(m => el.Name!.Contains(m, StringComparison.Ordinal)))
                return true;
            return el.FindAllDescendants()
                     .Any(d => d.Name != null && markers.Any(m => d.Name!.Contains(m, StringComparison.Ordinal)));
        }
        catch { return false; }
    }

    private static bool InvokeById(AutomationElement scope, string automationId)
    {
        var btn = scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        if (btn == null) return false;
        try { btn.AsButton().Invoke(); return true; }
        catch { return false; }
    }

    /// <summary>Poll a condition until it holds or the timeout passes.</summary>
    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try { if (condition()) return true; }
            catch { /* transient IO while the directory is being removed */ }
            Thread.Sleep(250);
        }
        try { return condition(); }
        catch { return false; }
    }

    private static void InstallSilently(string setup, string dir)
    {
        Directory.CreateDirectory(dir);
        var psi = new ProcessStartInfo(setup, $"/S /D={dir}") { UseShellExecute = false };
        using var p = Process.Start(psi)!;
        p.WaitForExit((int)Timeout.TotalMilliseconds * 3);
        Assert.True(File.Exists(Path.Combine(dir, "uninstall.exe")), "silent install produced no uninstaller");
    }

    private static string FindSetupPath()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor != null && !File.Exists(Path.Combine(cursor.FullName, "Vonvert.OSS.sln")))
            cursor = cursor.Parent;
        var root = cursor?.FullName
            ?? throw new FileNotFoundException("Repository root not found from " + AppContext.BaseDirectory);

        var setup = Path.Combine(root, "installer", "Output", "Vonvert_Setup.exe");
        if (!File.Exists(setup))
            throw new FileNotFoundException("Installer not found. Run: python build.py --package");
        return setup;
    }
}
