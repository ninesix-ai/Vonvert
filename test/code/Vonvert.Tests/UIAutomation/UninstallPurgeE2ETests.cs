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
/// packaged Vonvert_Setup.exe to a temp dir, seeds a probe folder under the default
/// data root, then drives the GUI uninstaller and answers the purge prompt:
///   * No  branch -> user data is kept
///   * Yes branch -> user data is purged
///
/// Prerequisites (NOT part of headless CI; run locally):
///   1. The App must wire the --purge-user-data command into OnStartup (Task 4), and
///      the installer must be built:  python build.py --package
///   2. An interactive desktop session (FlaUI drives real windows / #32770 dialogs).
///
/// A probe sub-directory is used so the real user data is never clobbered; both the
/// temp install dir and the probe are removed in a finally block.
/// </summary>
[Collection("UIAutomation")]
[Trait("Category", "UIAutomation")]
public sealed class UninstallPurgeE2ETests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    // Locale-independent Win32 MessageBox button ids.
    private const string MsgYesId = "6";
    private const string MsgNoId = "7";

    // The uninstall confirmation dialog exposes its primary "Uninstall" button as
    // control id 1 and Cancel as id 2 (verified against the built installer - not
    // the usual wizard Next=2/Cancel=3). Captions are localized, ids are not.
    private const string WizardPrimaryId = "1";
    private const string WizardCancelId = "2";

    // NSIS uses the dialog class for both the wizard window and the purge
    // MessageBox, so the class name alone cannot tell them apart - the prompt is
    // identified by its own message text. The Chinese entries are the localized
    // form of the uninstaller prompt (installer source: installer/setup.oss.nsi);
    // they must stay on the declaration line to remain inside the release scan's
    // allowlist for bilingual test data.
    private static readonly string[] PurgePromptMarkers = { "Also delete", "user data and settings", "是否同时删除", "用户数据" };

    private readonly UIA3Automation _auto = new();
    private readonly string _installDir;
    private readonly string _probeDir;   // lives under the default data root

    public UninstallPurgeE2ETests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), "vonvert-uninst-" + Guid.NewGuid().ToString("N"));
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ninesix-ai", "Vonvert");
        _probeDir = Path.Combine(defaultRoot, "_e2e_purge_probe");
    }

    public void Dispose()
    {
        // An NSIS uninstaller re-extracts itself under %TEMP%\ns*.tmp and runs as
        // the process name "Un", so a killed or failed run leaves a modal dialog
        // holding the (locked) install tree. Close those before deleting anything.
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
        try { if (Directory.Exists(_probeDir)) Directory.Delete(_probeDir, true); } catch { /* best-effort */ }
        _auto.Dispose();
    }

    private static string? SafePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch { return null; }   // exited, 32/64-bit mismatch or access denied
    }

    // These cases have never driven a real uninstall: the confirm dialog's button
    // ids (primary=1, cancel=2, verified against the built installer) are not
    // surfaced as the UIA AutomationId this matcher expects, so the earlier
    // name-caption lookup timed out silently and the No branch "passed" without
    // the uninstaller ever being answered.
    private const string BlockedOn = "uninstall wizard not reachable via UIA AutomationId yet; "
        + "needs an in-test UIA tree dump of the dialog, and before the Yes branch can run "
        + "a purge seam that keeps the default data root out of scope";

    [Fact(Skip = BlockedOn)]
    public void Uninstall_NoChoice_KeepsUserData()
    {
        RunUninstall(clickYes: false, expectPurged: false);
    }

    [Fact(Skip = BlockedOn)]
    public void Uninstall_YesChoice_PurgesUserData()
    {
        RunUninstall(clickYes: true, expectPurged: true);
    }

    private void RunUninstall(bool clickYes, bool expectPurged)
    {
        var setup = FindSetupPath();
        InstallSilently(setup, _installDir);

        // Seed a recognizable probe so presence/absence is unambiguous.
        Directory.CreateDirectory(_probeDir);
        File.WriteAllText(Path.Combine(_probeDir, "marker.json"), "{}");

        try
        {
            var uninstaller = Path.Combine(_installDir, "uninstall.exe");
            Assert.True(File.Exists(uninstaller), "uninstaller not produced by install");

            using var proc = Process.Start(uninstaller)!;
            DriveUninstallWizard(clickYes);

            // An NSIS uninstaller copies itself to a temp folder, so the process
            // started above can exit while the real work continues. Wait on the
            // observable outcome instead of trusting that handle.
            if (expectPurged)
            {
                bool gone = WaitUntil(() => !Directory.Exists(_probeDir), TimeSpan.FromSeconds(60));
                Assert.True(gone,
                    "Yes branch: purge prompt was answered but the data root is still present " +
                    $"({_probeDir}); check that the uninstaller runs --purge-user-data and that it exits 0");
            }
            else
            {
                WaitUntil(() => proc.HasExited, TimeSpan.FromSeconds(30));
                Assert.True(Directory.Exists(_probeDir), "No branch: expected data kept but probe is gone");
            }
        }
        finally
        {
            // Uninstall may have already removed the install dir; ensure probe is gone.
            try { if (Directory.Exists(_probeDir)) Directory.Delete(_probeDir, true); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Start the removal from the NSIS confirm page, then answer the purge prompt.
    /// The wizard and the prompt are both Win32 dialogs (class #32770), so the
    /// prompt is matched on its own message text while the wizard is driven by the
    /// locale-stable Next control id. Both branches must see the prompt: if it
    /// never shows, the uninstaller stopped asking and that is a product regression.
    /// </summary>
    private void DriveUninstallWizard(bool clickYes)
    {
        // The wizard and the purge prompt are both #32770 dialogs. The wizard is
        // the one carrying control ids 1 and 2; the prompt is matched on its own
        // message text and on the Yes button (id 6) a MB_YESNO box provides.
        var wizard = WaitForWindow(w => IsDialog(w)
                                        && HasControl(w, WizardPrimaryId)
                                        && HasControl(w, WizardCancelId),
                                    TimeSpan.FromSeconds(20));
        Assert.NotNull(wizard);
        Assert.True(InvokeById(wizard!, WizardPrimaryId),
            $"uninstall confirmation exposes no primary button (id {WizardPrimaryId}); removal cannot be started");

        var box = WaitForWindow(w => IsDialog(w)
                                      && HasControl(w, MsgYesId)
                                      && DialogTextMatches(w, PurgePromptMarkers),
                                TimeSpan.FromSeconds(20));
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
