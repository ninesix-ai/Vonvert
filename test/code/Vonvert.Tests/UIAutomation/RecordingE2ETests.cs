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
/// End-to-end UI automation for the recording + export feature: launches the real
/// Vonvert.exe, switches to the Recording tab through the sidebar rail, records a
/// short take, then opens the Export dialog for the resulting history row and
/// cancels it.
///
/// Environment-sensitive: needs an interactive desktop. Excluded from headless CI
/// via [Trait("Category", "UIAutomation")]; run locally with
///   dotnet test --filter "Category=UIAutomation"
///
/// Selectors use stable AutomationIds (x:Name is surfaced as the WPF AutomationId)
/// and accept both en/zh control labels so the test is language-independent.
/// </summary>
[Collection("UIAutomation")]
[Trait("Category", "UIAutomation")]
public sealed class RecordingE2ETests : IDisposable
{
    private const int MaxLaunchAttempts = 3;
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(8);

    private readonly UIA3Automation _auto = new();
    private Application? _app;

    // Accept both UI languages so the run is locale-agnostic.
    private static readonly string[] CancelLabels = { "Cancel", "取消" };
    private static readonly string[] ExportLabels = { "Export", "导出" };
    private static readonly string[] ExportDialogTitles = { "Export Recording", "导出录音" };
    private static readonly string[] WarningTitles = { "Recording notice", "录音提示" };

    public RecordingE2ETests()
    {
        var exePath = FindExePath();
        Exception? lastFailure = null;

        for (int attempt = 1; attempt <= MaxLaunchAttempts; attempt++)
        {
            KillStrayVonvertInstances();
            try
            {
                _app = Application.Launch(exePath, "--test-mode");
                _app.WaitWhileMainHandleIsMissing(LaunchTimeout);
                if (_app.GetMainWindow(_auto) != null)
                    return;
                lastFailure = new InvalidOperationException("Vonvert launched but no main window appeared.");
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            try { _app?.Close(); } catch { /* best-effort */ }
            try { _app?.Dispose(); } catch { /* best-effort */ }
            _app = null;
        }

        throw lastFailure ?? new InvalidOperationException("Failed to launch Vonvert.exe.");
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { /* best-effort */ }
        try { _app?.Dispose(); } catch { /* best-effort */ }
        _auto?.Dispose();
    }

    [Fact(DisplayName = "E2E-REC-001: select Recording tab, record a take, open + cancel Export dialog")]
    public void RecordingTab_Record_And_TriggerExport()
    {
        var window = _app!.GetMainWindow(_auto);
        Assert.NotNull(window);

        // 1) Switch to the Recording tab via the sidebar rail.
        var navRecording = WaitFor(() => window.FindFirstDescendant(cf => cf.ByAutomationId("NavRecordingBtn")));
        Assert.NotNull(navRecording);
        navRecording!.Patterns.SelectionItem.Pattern.Select();

        // 2) The Recording view's Start button becomes the active surface.
        var startBtn = WaitFor(() => window.FindFirstDescendant(cf => cf.ByAutomationId("StartRecordBtn")));
        Assert.NotNull(startBtn);

        // 3) Start recording → confirm the privacy warning modal (a Win32
        //    MessageBox, detected by its #32770 class + Yes button id "6",
        //    both locale-independent).
        startBtn!.AsButton().Invoke();
        var confirm = WaitForMessageBox();
        Assert.NotNull(confirm);
        ClickMessageBoxYes(confirm!);

        // Give the capture a moment to write a few blocks, then stop.
        Thread.Sleep(500);
        var stopBtn = WaitFor(() => window.FindFirstDescendant(cf => cf.ByAutomationId("StopRecordBtn")));
        Assert.NotNull(stopBtn);
        stopBtn!.AsButton().Invoke();

        // 4) A history row appears with an Export button.
        var exportBtn = WaitFor(() =>
            window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                  .FirstOrDefault(b => NameMatches(b.Name, ExportLabels)));
        Assert.NotNull(exportBtn);
        exportBtn!.AsButton().Invoke();

        // 5) The Export dialog opens as a modal child window; assert then cancel.
        var dialog = WaitForWindowByTitle(ExportDialogTitles);
        Assert.NotNull(dialog);
        ClickButtonByAnyName(dialog!, CancelLabels);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private T? WaitFor<T>(Func<T?> find, int? timeoutMs = null) where T : class
    {
        var deadline = DateTime.UtcNow + (timeoutMs.HasValue
            ? TimeSpan.FromMilliseconds(timeoutMs.Value) : UiTimeout);
        while (DateTime.UtcNow < deadline)
        {
            T? found = null;
            try { found = find(); } catch { /* tree not ready yet */ }
            if (found is not null) return found;
            Thread.Sleep(100);
        }
        return null;
    }

    private static bool NameMatches(string? name, string[] labels)
        => name != null && labels.Any(l => name.Contains(l));

    /// <summary>
    /// Wait for the Win32 privacy-warning MessageBox. Detected as a #32770
    /// dialog anywhere on the desktop, with a warning-title match as fallback —
    /// both independent of the current UI language.
    /// </summary>
    private Window? WaitForMessageBox()
    {
        return WaitFor(() =>
        {
            try
            {
                var byClass = _auto.GetDesktop().FindFirstDescendant(cf => cf.ByClassName("#32770"));
                if (byClass != null) return byClass.AsWindow();
            }
            catch { /* tree not ready yet */ }

            foreach (var w in _auto.GetDesktop().FindAllChildren())
            {
                try { if (NameMatches(w.Name, WarningTitles)) return w.AsWindow(); }
                catch { /* window disposed mid-scan */ }
            }
            return null;
        }, timeoutMs: 6000);
    }

    /// <summary>Click the Yes button of a MessageBox via its locale-independent control id (6).</summary>
    private static void ClickMessageBoxYes(Window box)
    {
        var yes = box.FindFirstDescendant(cf => cf.ByAutomationId("6"));
        if (yes != null) { try { yes.AsButton().Invoke(); return; } catch { /* fall through */ } }
        box.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault()?.AsButton().Invoke();
    }

    private Window? WaitForWindowByTitle(string[] titles)
    {
        return WaitFor(() =>
        {
            try
            {
                var match = _auto.GetDesktop().FindAllChildren()
                    .FirstOrDefault(w => NameMatches(w.Name, titles));
                return match?.AsWindow();
            }
            catch { return null; }
        }, timeoutMs: 5000);
    }

    private static void ClickButtonByAnyName(Window window, string[] names)
    {
        var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var btn = buttons.FirstOrDefault(b => NameMatches(b.Name, names));
        if (btn != null) { try { btn.AsButton().Invoke(); return; } catch { /* click is best-effort */ } }
        // Fall back to the first button if no localized name matched.
        try { buttons.FirstOrDefault()?.AsButton().Invoke(); } catch { /* best-effort */ }
    }

    private static string FindExePath()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor != null && !File.Exists(Path.Combine(cursor.FullName, "Vonvert.OSS.sln")))
            cursor = cursor.Parent;
        var root = cursor?.FullName
            ?? throw new FileNotFoundException("Repository root not found from " + AppContext.BaseDirectory);

        var binDir = Path.Combine(root, "Vonvert.App", "bin");
        if (!Directory.Exists(binDir))
            throw new FileNotFoundException("Vonvert.App/bin not found. Run build first.");

        var currentCfg = AppContext.BaseDirectory.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase)
            ? "Release" : "Debug";
        foreach (var cfg in new[] { currentCfg, currentCfg == "Release" ? "Debug" : "Release" })
        {
            var exe = Directory.GetFiles(binDir, "Vonvert.exe", SearchOption.AllDirectories)
                .FirstOrDefault(p =>
                    p.IndexOf($"\\{cfg}\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    p.IndexOf("\\publish\\", StringComparison.OrdinalIgnoreCase) < 0);
            if (exe != null) return exe;
        }
        throw new FileNotFoundException("Vonvert.exe not found. Run build first.");
    }

    private static void KillStrayVonvertInstances()
    {
        foreach (var p in Process.GetProcessesByName("Vonvert"))
        {
            try { p.Kill(); p.WaitForExit(2000); }
            catch { /* stale instance already gone */ }
            finally { p.Dispose(); }
        }
    }
}
