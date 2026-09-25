// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Engine.Soundboard;
using Vonvert.App.Controls;
using Vonvert.App.UIServices;

namespace Vonvert.App;

public partial class App : Application
{
    public static VoiceEngine      Engine     { get; private set; } = null!;
    public static AudioDeviceHub    Devices    { get; private set; } = null!;
    public static PresetManager    Presets    { get; private set; } = null!;
    public static HotkeyService?   Hotkeys    { get; private set; }
    public static RecordingService? Recording { get; private set; }
    public static SoundboardManager? Soundboard { get; private set; }
    public static SoundboardAuditionPlayer? Audition { get; private set; }

    // Register in the constructor — fires BEFORE InitializeComponent() and OnStartup
    // so we catch EVERY possible exception including BAML resource loading failures
    public App()
    {
        // Initialize logging FIRST so all subsequent events are captured
        AppLog.Init();

        DispatcherUnhandledException += AppDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += AppDomainException;
    }

    private void AppDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string log = BuildCrashLog(e.Exception);
        WriteCrashLog(log);
        AppLog.Error(e.Exception, "Unhandled dispatcher exception: {Message}", e.Exception.Message);
        MessageBox.Show(
            $"Error (see crash log at %TEMP%\\Vonvert_crash.txt):\n\n{e.Exception.Message}\n\nat: {e.Exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}",
            "Vonvert Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void AppDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        string msg = e.ExceptionObject is Exception ex ? BuildCrashLog(ex) : e.ExceptionObject?.ToString() ?? "Unknown";
        WriteCrashLog(msg);
        if (e.ExceptionObject is Exception exObj)
            AppLog.Error(exObj, "Fatal AppDomain exception: {Message}", exObj.Message);
        else
            AppLog.Error("Fatal AppDomain exception: {Msg}", msg);
        AppLog.Flush(); // ensure error is on disk before process terminates
        MessageBox.Show($"Fatal startup crash:\n\n{msg.Split('\n')[0]}", "Vonvert Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string BuildCrashLog(Exception ex)
    {
        string log = $"=== Vonvert Crash Report ===\n{DateTime.Now}\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        // Add XAML-specific details if available
        if (ex is System.Windows.Markup.XamlParseException xamlEx)
        {
            log += $"\n\n--- XAML Details ---\n";
            log += $"  LineNumber:    {xamlEx.LineNumber}\n";
            log += $"  LinePosition:  {xamlEx.LinePosition}\n";
            log += $"  BaseUri:       {xamlEx.BaseUri}\n";
        }
        if (ex.InnerException != null)
        {
            log += $"\n--- Inner ---\n{ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            // Walk the full inner exception chain
            var inner = ex.InnerException.InnerException;
            while (inner != null)
            {
                log += $"\n\n--- Inner Inner ---\n{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}";
                inner = inner.InnerException;
            }
        }
        return log;
    }

    private static void WriteCrashLog(string content)
    {
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "Vonvert_crash.txt"), content); } catch { /* crash dump write is best-effort */ }
    }

    private static Mutex? _singleInstanceMutex;
    private static bool _ownsMutex = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Information("OnStartup begin");

        // Uninstaller-invoked data purge: run before the single-instance mutex so
        // cleanup can execute without launching the GUI or contending for the lock.
        if (StartupArgs.IsPurgeMode(e.Args))
        {
            var purge = new Vonvert.Engine.UserDataPurger().Purge(
                Vonvert.Engine.PurgeOptions.FromEnvironment());
            AppLog.Information($"[Purge] deleted={purge.DeletedPaths.Count} " +
                $"skipped={purge.SkippedPaths.Count} failed={purge.FailedPaths.Count}");
            Environment.Exit(purge.ExitCode);
            return;
        }

        bool isNewInstance;
        _singleInstanceMutex = new Mutex(true, "Vonvert_SingleInstance_Mutex", out isNewInstance);
        if (!isNewInstance)
        {
            // Another instance marker exists. Try to bring a LIVE instance's
            // visible window to the front FIRST. Only shut down if we actually
            // foregrounded a real window. Otherwise (e.g. the mutex is only a
            // stale/crashed 0-thread leftover, or the other window is hidden
            // and cannot be brought forward) DO NOT silently exit - show the
            // UI anyway so the user never sees an empty launch.
            bool activatedExisting = false;
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("Vonvert");
                try
                {
                    foreach (var p in processes)
                    {
                        // Only a live process with an actual window can present UI.
                        if (p.Id != System.Diagnostics.Process.GetCurrentProcess().Id
                            && p.Threads.Count > 0
                            && p.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(p.MainWindowHandle, SW_RESTORE);
                            SetForegroundWindow(p.MainWindowHandle);
                            activatedExisting = true;
                            // Don't break — dispose all remaining Process objects below
                        }
                    }
                }
                finally
                {
                    // Dispose ALL Process objects to prevent handle leaks
                    foreach (var p in processes) p.Dispose();
                }
            }
            catch { /* single-instance detection is best-effort */ }

            if (activatedExisting)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            // No live window existed - take over as the owning instance and
            // fall through to create and show the main window below.
        }
        _ownsMutex = true;

        // Load saved language preference before creating the main window
        LocalizationManager.Instance.LoadSavedLanguage();

        // ── Layout config (JSON-driven UI sizing) ──
        LayoutConfig.Instance.ApplyToResources();

        // ── Fix ShutdownMode bug: set OnExplicitShutdown before showing dialogs ──
        // This prevents the app from shutting down when the dialog closes
        // (which happens because MainWindow hasn't been created yet)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Create and show MainWindow FIRST (same as v1) - before heavy audio
        // service initialization - so the UI always appears immediately.
        MainWindow window;
        try
        {
            window = new MainWindow();
        }
        catch (Exception ex)
        {
            // If we let this propagate, AppDispatcherException marks it Handled
            // and the process survives headless (no window, no tray) while still
            // holding the single-instance mutex — a zombie that blocks every
            // future launch. Exit cleanly instead.
            AppLog.Error(ex, "MainWindow creation failed — shutting down");
            MessageBox.Show(
                $"Vonvert failed to start:\n\n{ex.Message}\n\nSee log at {Path.Combine(AppPaths.Root, "logs")}",
                "Vonvert Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        // ── Global Ctrl+C: copy text from any TextBlock ──
        EventManager.RegisterClassHandler(typeof(Window),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnGlobalPreviewKeyDown),
            handledEventsToo: true);

        MainWindow = window;
        // Restore normal shutdown behavior now that MainWindow exists
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        window.Activate();

        // Defer the heavy initialization so the window renders FIRST, then
        // wire services/audio on a background thread. Running AudioDeviceHub /
        // VoiceEngine / DiscordRpc synchronously on the UI thread blocks the
        // WPF dispatcher from pumping, which leaves the window a black blank
        // surface (empty visual tree) until OnStartup returns.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                // All user data (presets, config, logs, …) lives under the single,
                // user-relocatable AppPaths.Root — see Vonvert.Engine.AppPaths.
                string presetFolder = Path.Combine(AppPaths.Root, "Presets");

                AppLog.Information("Initializing AudioDeviceHub...");
                Devices = new AudioDeviceHub();
                AppLog.Information("AudioDeviceHub done");

                AppLog.Information("Initializing VoiceEngine...");
                Engine  = new VoiceEngine();
                AppLog.Information("VoiceEngine done");

                // File recorder fed by the real-time pipeline (Original/Processed modes).
                Recording = new RecordingService();
                (Engine.Pipeline as NullAudioProcessor)?.AttachRecordingService(Recording);
                AppLog.Information("RecordingService attached");

                // Soundboard one-shot catalogue. Live-mode playback routes through the
                // engine mixer; local audition owns a playback handle released in OnExit.
                Soundboard = new SoundboardManager();
                // Local-only audition channel, independent of the voice engine.
                Audition = new SoundboardAuditionPlayer();
                Soundboard.Audition = Audition;
                Soundboard.LiveMode = AppConfig.Instance.Audio.SoundboardLiveMode;
                AppLog.Information("SoundboardManager initialized (liveMode={Live})", Soundboard.LiveMode);

                // Restore the user's device selection (mic / VB-Cable) if the
                // saved devices still exist on this machine.
                var savedAudio = AppConfig.Instance.Audio;
                if (!string.IsNullOrEmpty(savedAudio.InputDeviceId)
                    && Devices.Resolve(savedAudio.InputDeviceId) != null)
                    Engine.Settings.InputDeviceId = savedAudio.InputDeviceId;
                if (!string.IsNullOrEmpty(savedAudio.OutputDeviceId)
                    && Devices.Resolve(savedAudio.OutputDeviceId) != null)
                    Engine.Settings.OutputDeviceId = savedAudio.OutputDeviceId;

                AppLog.Information("Initializing PresetManager (folder: {Folder})", presetFolder);
                Presets = new PresetManager(presetFolder);
                AppLog.Information("Presets done");

                // Global hotkey service (RegisterHotKey + PTT keyboard hook). Created
                // here; attached to the window handle by MainWindow.BindHotkeys().
                Hotkeys = new HotkeyService();

                // Surface preset-index save failures to the user (one-time).
                bool indexSaveWarned = false;
                Presets.Index.SaveFailed += path =>
                {
                    if (indexSaveWarned) return;
                    indexSaveWarned = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            $"Failed to save preset index to:\n{path}\n\nYour presets are still in memory, but recent changes may be lost on exit.",
                            "Vonvert — Preset Index Save Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }));
                };

                // All services initialized successfully
                AppLog.Information("All services initialized successfully");

                // Warn the user if no audio devices are detected — the engine
                // will run but cannot capture or output anything.
                if (Devices.InputDevices().Count == 0 && Devices.OutputDevices().Count == 0)
                {
                    AppLog.Warning("No audio input or output devices detected at startup");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            "No audio devices were detected on your system.\n\n" +
                            "Vonvert requires at least one audio input or output device to function.\n" +
                            "Please check your audio drivers and connected devices.",
                            "Vonvert — No Audio Devices",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }));
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Failed to initialize audio services");
                WriteCrashLog(BuildCrashLog(ex));
                MessageBox.Show(
                    $"Failed to initialize audio services:\n\n{ex.Message}\n\nSee crash log at %TEMP%\\Vonvert_crash.txt",
                    "Vonvert Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // Services are ready - hook them into the window (engine auto-start,
            // devices, presets, visualizers...).
            AppLog.Information("Before OnServicesInitialized");
            window.OnServicesInitialized();
            AppLog.Information("After OnServicesInitialized");
        }));
    }

    // SelectableTextBlock (inherits TextBox) has native Ctrl+C support via
    // ApplicationCommands.Copy, so we only need to handle plain TextBlocks
    // that remain inside ControlTemplates or have inline Run elements.
    private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        var focused = Keyboard.FocusedElement as DependencyObject;

        // 1) SelectableTextBlock with selection → let built-in Copy handle it
        if (focused is SelectableTextBlock)
            return;

        // 2) Focused TextBlock → copy its full text
        if (focused is TextBlock focusedTb && !string.IsNullOrEmpty(focusedTb.Text))
        {
            Clipboard.SetText(focusedTb.Text);
            e.Handled = true;
            return;
        }

        // 3) TextBox with selection → let the built-in Copy command handle it
        if (focused is TextBox tb && tb.SelectionLength > 0)
            return;

        // 4) Fallback: hit-test at mouse position for TextBlock under cursor
        var win = (sender as Window) ?? MainWindow;
        if (win == null) return;

        try
        {
            var mousePos = Mouse.GetPosition(win);
            var hitResult = VisualTreeHelper.HitTest(win, mousePos);
            var dep = hitResult?.VisualHit as DependencyObject;

            while (dep != null)
            {
                // SelectableTextBlock has native copy — skip it
                if (dep is SelectableTextBlock)
                    return;

                if (dep is TextBlock tbHit && !string.IsNullOrEmpty(tbHit.Text))
                {
                    Clipboard.SetText(tbHit.Text);
                    e.Handled = true;
                    return;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }
        catch { /* hit-test may fail in certain visual states */ }
    }

    // Win32 helpers for restoring another instance's window
    private const int SW_RESTORE = 9;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override async void OnExit(ExitEventArgs e)
    {
        AppLog.Information("=== Vonvert shutting down (exit code: {Code}) ===", e.ApplicationExitCode);

        // Wrap entire teardown in an overall timeout to prevent zombie-process
        // scenarios where a blocked Dispose keeps the PID alive indefinitely.
        var exitTask = Task.Run(() =>
        {
            // Safety net: release MainWindow-owned external services (e.g. Discord RPC)
            // in case the app exited via a path that bypassed MainWindow.OnClosing.
            // MainWindow.CleanupExternalServices is idempotent — safe to call again.
            try { (MainWindow as Vonvert.App.MainWindow)?.CleanupExternalServices(); } catch { /* cleanup is best-effort (idempotent) */ }
            try { Engine?.Stop(); Engine?.Dispose(); } catch { /* engine dispose is best-effort */ }
            try { Devices?.Dispose(); } catch { /* audio devices dispose is best-effort */ }
            try { Recording?.Dispose(); } catch { /* recording dispose is best-effort */ }
            try { Audition?.Dispose(); } catch { /* audition dispose is best-effort */ }

            try
            {
                if (_singleInstanceMutex != null && _ownsMutex)
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                _singleInstanceMutex?.Dispose();
            }
            catch { /* mutex release is best-effort */ }
        });

        // Await asynchronously — does not block the UI thread.
        // All windows are already closed at this point so there is no visible
        // impact, but the OS shutdown path no longer stalls on this thread.
        if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10))) != exitTask)
        {
            AppLog.Warning("OnExit timed out after 10s — forcing shutdown");
            AppLog.Shutdown();
            Environment.Exit(0);
            return;
        }

        AppLog.Shutdown();

        base.OnExit(e);
    }
}
