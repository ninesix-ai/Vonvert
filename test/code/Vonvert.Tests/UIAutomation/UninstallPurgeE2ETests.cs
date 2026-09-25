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
    private static readonly string[] ConfirmYesNames = { "Yes", "是", "&Yes", "是(&Y)" };

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
        try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { /* best-effort */ }
        try { if (Directory.Exists(_probeDir)) Directory.Delete(_probeDir, true); } catch { /* best-effort */ }
        _auto.Dispose();
    }

    [Fact]
    public void Uninstall_NoChoice_KeepsUserData()
    {
        RunUninstall(clickYes: false, expectPurged: false);
    }

    [Fact]
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
            proc.WaitForExit((int)Timeout.TotalMilliseconds * 3);

            var probeStillThere = Directory.Exists(_probeDir);
            if (expectPurged)
                Assert.False(probeStillThere, "Yes branch: expected purge but probe remains");
            else
                Assert.True(probeStillThere, "No branch: expected data kept but probe is gone");
        }
        finally
        {
            // Uninstall may have already removed the install dir; ensure probe is gone.
            try { if (Directory.Exists(_probeDir)) Directory.Delete(_probeDir, true); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Advance the NSIS uninstall wizard past its confirmation page, then answer the
    /// purge #32770 prompt with the requested button. The purge prompt may be skipped
    /// entirely if the app-side purge command is not yet wired, so absence is tolerated
    /// for the No branch but asserted for the Yes branch.
    /// </summary>
    private void DriveUninstallWizard(bool clickYes)
    {
        // 1) Click the wizard's Yes/OK confirm (MUI_UNPAGE_CONFIRM) to begin removal.
        var deadline = DateTime.UtcNow + Timeout;
        bool clickedConfirm = false;
        while (DateTime.UtcNow < deadline && !clickedConfirm)
        {
            clickedConfirm = TryClickNamedButton(ConfirmYesNames);
            if (!clickedConfirm) Thread.Sleep(250);
        }

        // 2) Wait for the purge MessageBox (#32770) and answer it.
        var box = WaitForPurgeMessageBox(TimeSpan.FromSeconds(10));
        if (box != null)
            ClickMessageBoxButton(box, clickYes ? MsgYesId : MsgNoId);
        else if (clickYes)
            Assert.Fail("Yes branch: purge prompt never appeared (is --purge-user-data wired into the app and packaged?).");
    }

    private Window? WaitForPurgeMessageBox(TimeSpan wait)
    {
        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var el = _auto.GetDesktop().FindFirstDescendant(cf => cf.ByClassName("#32770"));
                if (el != null) return el.AsWindow();
            }
            catch { /* tree not ready */ }
            Thread.Sleep(200);
        }
        return null;
    }

    private static void ClickMessageBoxButton(Window box, string automationId)
    {
        var btn = box.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        if (btn != null) { try { btn.AsButton().Invoke(); return; } catch { /* fall through */ } }
        // Fallback: Yes is the first button, No the second in a MB_YESNO box.
        var buttons = box.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var pick = automationId == MsgYesId ? buttons.FirstOrDefault() : buttons.Skip(1).FirstOrDefault();
        try { pick?.AsButton().Invoke(); } catch { /* best-effort */ }
    }

    private bool TryClickNamedButton(string[] names)
    {
        try
        {
            foreach (var w in _auto.GetDesktop().FindAllChildren())
            {
                var match = w.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => b.Name != null && names.Any(n => b.Name!.Contains(n, StringComparison.OrdinalIgnoreCase)));
                if (match != null) { try { match.AsButton().Invoke(); return true; } catch { /* ignore */ } }
            }
        }
        catch { /* window scan */ }
        return false;
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
