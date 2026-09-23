// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace Vonvert.Tests.UIAutomation;

/// <summary>
/// Basic end-to-end UI automation test using FlaUI.
/// Launches the real Vonvert.exe and verifies the main window is visible.
/// Run locally — CI can skip via [Trait("Category", "UIAutomation")].
/// </summary>
[Collection("UIAutomation")]
[Trait("Category", "UIAutomation")]
public sealed class MainWindowE2ETests : IDisposable
{
    private const int MaxLaunchAttempts = 3;
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(10);

    private Application? _app;
    private readonly UIA3Automation _auto;

    public MainWindowE2ETests()
    {
        _auto = new UIA3Automation();

        // The app enforces a single-instance mutex (App.xaml.cs). A stale
        // Vonvert.exe left behind by a previous crashed/aborted run makes a
        // freshly launched process call Shutdown() and exit immediately, which
        // FlaUI then surfaces as "Process with an Id ... is not running" when the
        // rest of the suite runs in the same batch (the test passes when run
        // alone, because no stray instance is present). Clear any strays and
        // retry the launch a few times so the E2E bootstrap is robust to that race.
        var exePath = FindExePath();
        Exception? lastFailure = null;

        for (int attempt = 1; attempt <= MaxLaunchAttempts; attempt++)
        {
            KillStrayVonvertInstances();
            try
            {
                // Launch with --test-mode to skip WASAPI device init
                _app = Application.Launch(exePath, "--test-mode");
                _app.WaitWhileMainHandleIsMissing(LaunchTimeout);
                if (_app.GetMainWindow(_auto) != null)
                    return;   // success — main window is up

                lastFailure = new InvalidOperationException("Vonvert launched but no main window appeared.");
            }
            catch (Exception ex)
            {
                lastFailure = ex;
            }

            // Tear down this attempt before retrying.
            try { _app?.Close(); } catch { /* best-effort */ }
            try { _app?.Dispose(); } catch { /* best-effort */ }
            _app = null;
        }

        throw lastFailure
            ?? new InvalidOperationException($"Failed to launch Vonvert.exe after {MaxLaunchAttempts} attempts.");
    }

    /// <summary>
    /// Kill leftover Vonvert.exe processes so the single-instance mutex is free
    /// before the next launch. Best-effort: a process may exit between the
    /// snapshot and Kill(); failures are ignored.
    /// </summary>
    private static void KillStrayVonvertInstances()
    {
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Vonvert"))
        {
            try
            {
                p.Kill();
                p.WaitForExit(2000);
            }
            catch { /* stale instance already gone or not killable — ignore */ }
            finally { p.Dispose(); }
        }
    }

    public void Dispose()
    {
        _app?.Close();
        _app?.Dispose();
        _auto?.Dispose();
    }

    private static string FindExePath()
    {
        // Walk up from the test output dir until the repository root
        // (identified by the solution file) is found — this is robust to the
        // bin layout varying with build config / runtime (x64 sub-folder etc.).
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor != null && !File.Exists(Path.Combine(cursor.FullName, "Vonvert.OSS.sln")))
            cursor = cursor.Parent;
        var root = cursor?.FullName
            ?? throw new FileNotFoundException("Repository root not found from " + AppContext.BaseDirectory);

        var binDir = Path.Combine(root, "Vonvert.App", "bin");
        if (!Directory.Exists(binDir))
            throw new FileNotFoundException("Vonvert.App/bin not found. Run build first.");

        // Prefer the configuration the tests are running in (Debug locally,
        // Release on CI), fall back to the other one.
        var currentCfg = AppContext.BaseDirectory.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
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

    private Window GetMainWindow()
        => (_app?.GetMainWindow(_auto)) ?? throw new InvalidOperationException("No main window");

    [Fact]
    public void E2E001_AppLaunches_WindowIsVisible()
    {
        var window = GetMainWindow();
        Assert.True(window.IsOffscreen == false, "Main window should be visible");
    }
}
