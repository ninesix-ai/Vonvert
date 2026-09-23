// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows.Shapes;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.App.Services;

namespace Vonvert.App;

/// <summary>
/// Main window — real-time voice changer.
/// Layout: header (window controls) → spectrum/pitch strip → preset tiles → bottom bar.
/// No settings, no recording, no soundboard, no offline conversion.
/// </summary>
public partial class MainWindow : Window, IAppServices
{
    private static LocalizationManager L => LocalizationManager.Instance;
    // 100 ms refresh — VU meter + spectrum are perceptually smooth at this rate.
    private readonly DispatcherTimer _metricsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    // GitHub release update checker (badge in the bottom bar)
    private readonly UpdateChecker _updateChecker = new();
    private bool _isEffectOn  = false;
    private bool _uiReady     = false;  // guards all event handlers against XAML-init-time fires
    private bool _cleanupDone = false;  // guards against double-execution of CleanupForExit
    private bool _servicesInitialized = false;
    private int ResizeBorderWidth => AppConfig.Instance.Ui.ResizeBorderWidth;
    private HwndSource? _hwndSource;

    private float _lastGain = DspDefaults.PreAmpGain;  // gain restored on preset apply

    // Spectrum visualization (state + rendering extracted to SpectrumVisualizer)
    private SpectrumVisualizer? _spectrumViz;

    // Pitch visualization
    private readonly Polyline _pitchCurveLine = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(180, 123, 93, 255)),
        StrokeThickness = 1.5,
        Stretch = Stretch.Fill
    };
    private bool _pitchCurveInitialized;

    // Pre-initialized so XAML ValueChanged/Checked events during InitializeComponent never crash.
    // BindDSPEffects() replaces these with the real engine instances on window load.
    private VxPitch   _pitch   = new();
    private VxReverb  _reverb  = new();
    private VxChorus  _chorus  = new();
    private VxCompressor _comp = new();
    private VxGate    _gate    = new();
    private VxEq      _eq      = new();
    private NoiseReductionEffect _noiseRed = new();
    private DelayEffect _delay  = new();
    private DeesserEffect _deesser = new();
    private VxRobot   _robot   = new();
    private VxDrive   _drive   = new();
    private TiltEQEffect    _tiltEq    = new();
    private GraphicEQEffect _graphicEq = new();
    private BitcrusherEffect _bitcrusher = new();
    private FlangerEffect   _flanger   = new();
    private PhaserEffect    _phaser    = new();
    private TremoloEffect   _tremolo   = new();
    private VibratoEffect   _vibrato   = new();

    /// <summary>Centralized VoiceProfile ↔ Engine parameter mapping (WPF-free, testable).</summary>
    private DspParameterCoordinator? _dspCoordinator;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception diagEx)
        {
            try { AppLog.Error(diagEx, "MainWindow InitializeComponent failed: {Msg}", diagEx.Message); } catch { /* diagnostic logging must not throw */ }
            throw;
        }

        // Enable edge-resize on borderless window via WM_NCHITTEST
        SourceInitialized += MainWindow_SourceInitialized;

        // Everything that could fail goes in Loaded — after the window handle exists
        Loaded += OnWindowLoaded;

        AppLog.Debug("MainWindow ctor: Content={Type}", this.Content?.GetType().Name ?? "NULL");
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Pure-UI initialization that does not depend on audio services.
        try { StartMetricsTimer(); } catch { /* metrics timer is non-critical */ }

        // Subscribe to bottom bar events
        if (BottomBar != null)
        {
            BottomBar.EngineToggleRequested += TogglePower;
            BottomBar.UpdateBadgeClicked += OnUpdateBadgeClicked;
        }

        // Sidebar nav (Voice Change / Settings) — Checked-driven tab switching
        if (SidebarNav != null)
            SidebarNav.TabRequested += OnNavTabRequested;

        _uiReady = true;  // now safe for all event handlers to run

        // Start checking for updates on GitHub (non-blocking, best-effort)
        try
        {
            _updateChecker.UpdateAvailable += OnUpdateAvailable;
            _updateChecker.Start();
        }
        catch (Exception ex) { AppLog.Warning(ex, "UpdateChecker start failed"); }

        UpdateDynamicStrings();
        UpdateTrayMenuStrings();

        // Services may already be initialized (if App.OnStartup created them
        // before showing the window). Otherwise App.OnStartup will call
        // OnServicesInitialized() once they are ready.
        if (App.Engine != null && App.Devices != null)
            OnServicesInitialized();
    }

    public void OnServicesInitialized()
    {
        if (_servicesInitialized) return;   // guard: Loaded + deferred startup can both call this
        _servicesInitialized = true;
        AppLog.Debug("OnServicesInitialized begin");
        try { BindDSPEffects(); }     catch (Exception ex) { AppLog.Error(ex, "BindDSPEffects failed"); ShowNotification(L.ErrDspInit + ex.Message, "error"); }
        UpdateMicBanner();
        UpdateVoiceToggleVisuals(_isEffectOn);
        // PresetPickerControl builds its own tiles on Loaded; wire up events here
        try
        {
            if (MainPresetPicker != null)
            {
                MainPresetPicker.PresetChanged += MainPresetPicker_PresetChanged;
                MainPresetPicker.ImportRequested += OnImportPreset;
                MainPresetPicker.ExportRequested += OnExportPreset;
                MainPresetPicker.DeleteRequested += OnDeletePreset;
                // Loaded may have fired before App.Presets was ready; rebuild now.
                MainPresetPicker.RefreshTiles();
            }
            if (ExpertPanel != null)
                ExpertPanel.ParameterChanged += OnExpertParameterChanged;
            if (ExpertModeCombo != null)
                ExpertModeCombo.SelectedIndex = 0;   // default to Simple mode
        } catch (Exception ex) { AppLog.Warning(ex, "MainPresetPicker init failed"); }
        try { InitSpectrumVisualizer(); } catch (Exception ex) { AppLog.Warning(ex, "InitSpectrumVisualizer failed"); }

        // Out-of-the-box experience: auto-apply the single built-in preset
        // (Female) and enable the effect, so speaking into the mic is
        // transformed immediately without requiring the user to find the
        // power toggle first.
        try
        {
            if (MainPresetPicker != null)
            {
                var defaultName = L.GetPresetDisplayName("Female");
                _suppressParamExpand = true;   // seed the panel silently; don't slide the rail open on startup
                try { MainPresetPicker.SetPresetByName(defaultName); }
                finally { _suppressParamExpand = false; }
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "Auto-apply default preset failed"); }

        _isEffectOn = true;
        App.Engine.SetCompareMode(CompareMode.Normal);
        UpdateVoiceToggleVisuals(on: true);
        UpdateCompareSegments(CompareMode.Normal);
        AppLog.Debug("OnServicesInitialized: starting engine on background thread");
        // Start the engine on a background thread so audio graph init (WASAPI)
        // never blocks the UI thread / render pass.
        var engineToStart = App.Engine;
        var devicesToStart = App.Devices;
        System.Threading.Tasks.Task.Run(() =>
        {
            try { engineToStart.Start(devicesToStart); }
            catch (Exception ex) { AppLog.Error(ex, "Engine.Start failed on background thread"); AppLog.Flush(); }
        });
        AppLog.Debug("OnServicesInitialized: engine start queued");

        // Attach global hotkeys to the now-existing window handle (power / mute /
        // next-preset / push-to-talk) and enable Settings re-binding capture.
        try { BindHotkeys(); } catch (Exception ex) { AppLog.Warning(ex, "BindHotkeys failed"); }

        // Use InvokeAsync (not Invoke) to prevent deadlocks during shutdown.
        App.Devices.DevicesChanged += OnDeviceListChangedWrapper;
        App.Engine.DeviceLost += OnEngineDeviceDisconnectedWrapper;
    }

    // Named delegates so we can detach them cleanly during shutdown
    private void OnDeviceListChangedWrapper() => Dispatcher.InvokeAsync(() =>
    {
        if (_servicesInitialized) { try { UpdateMicBanner(); CheckVBCable(); } catch { /* device refresh on hot-plug is best-effort */ } }
    });

    private void OnEngineDeviceDisconnectedWrapper() => Dispatcher.InvokeAsync(() =>
    {
        if (_servicesInitialized)
        {
            ShowNotification(L.MicDisconnected, "error");
            try { UpdateMicBanner(); } catch { /* device refresh after disconnect is best-effort */ }
        }
    });

    // ════ DSP Effect References ════

    private void BindDSPEffects()
    {
        var chain = App.Engine.Effects;
        _pitch      = chain.Effects.OfType<VxPitch>().FirstOrDefault()          ?? new VxPitch();
        _reverb     = chain.Effects.OfType<VxReverb>().FirstOrDefault()         ?? new VxReverb();
        _chorus     = chain.Effects.OfType<VxChorus>().FirstOrDefault()         ?? new VxChorus();
        _comp       = chain.Effects.OfType<VxCompressor>().FirstOrDefault()     ?? new VxCompressor();
        _gate       = chain.Effects.OfType<VxGate>().FirstOrDefault()           ?? new VxGate();
        _eq         = chain.Effects.OfType<VxEq>().FirstOrDefault()             ?? new VxEq();
        _noiseRed   = chain.Effects.OfType<NoiseReductionEffect>().FirstOrDefault() ?? new NoiseReductionEffect();
        _delay      = chain.Effects.OfType<DelayEffect>().FirstOrDefault()      ?? new DelayEffect();
        _deesser    = chain.Effects.OfType<DeesserEffect>().FirstOrDefault()    ?? new DeesserEffect();
        _robot      = chain.Effects.OfType<VxRobot>().FirstOrDefault()          ?? new VxRobot();
        _drive      = chain.Effects.OfType<VxDrive>().FirstOrDefault()          ?? new VxDrive();
        _tiltEq     = chain.Effects.OfType<TiltEQEffect>().FirstOrDefault()     ?? new TiltEQEffect();
        _graphicEq  = chain.Effects.OfType<GraphicEQEffect>().FirstOrDefault()  ?? new GraphicEQEffect();
        _bitcrusher = chain.Effects.OfType<BitcrusherEffect>().FirstOrDefault() ?? new BitcrusherEffect();
        _flanger    = chain.Effects.OfType<FlangerEffect>().FirstOrDefault()     ?? new FlangerEffect();
        _phaser     = chain.Effects.OfType<PhaserEffect>().FirstOrDefault()      ?? new PhaserEffect();
        _tremolo    = chain.Effects.OfType<TremoloEffect>().FirstOrDefault()     ?? new TremoloEffect();
        _vibrato    = chain.Effects.OfType<VibratoEffect>().FirstOrDefault()     ?? new VibratoEffect();

        // Build the centralized parameter coordinator (WPF-free, testable)
        _dspCoordinator = new DspParameterCoordinator(
            App.Engine, _pitch, _reverb, _chorus, _comp, _gate, _eq, _noiseRed, _delay, _deesser, _robot, _drive,
            _tiltEq, _graphicEq, _bitcrusher, _flanger, _phaser, _tremolo, _vibrato);
    }

    // ════ Power ════

    private void TogglePower()
    {
        if (!_isEffectOn)
        {
            App.Engine.SetCompareMode(CompareMode.Normal);
            if (App.Engine.State != EngineStatus.Active) App.Engine.Start(App.Devices);

            _isEffectOn = true;
            UpdateVoiceToggleVisuals(on: true);
            ShowToast(L.ToastPowerOn, "success");
            UpdateCompareSegments(CompareMode.Normal);
        }
        else
        {
            // Power off = raw passthrough (Dry), so the user hears their
            // unmodified mic signal until they toggle back on.
            App.Engine.SetCompareMode(CompareMode.Dry);
            _isEffectOn = false;
            UpdateVoiceToggleVisuals(on: false);
            ShowToast(L.ToastPowerOff, "info");
            UpdateCompareSegments(CompareMode.Dry);
        }
    }

    // ── A/B Compare: segmented control ──────────────────────────────

    private void CompareSegment_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;

        var mode = tag switch
        {
            "Dry"     => CompareMode.Dry,
            "WetOnly" => CompareMode.WetOnly,
            _         => CompareMode.Normal
        };

        App.Engine.SetCompareMode(mode);
        UpdateCompareSegments(mode);
    }

    private void UpdateCompareSegments(CompareMode mode)
    {
        if (SegDry == null || SegNormal == null || SegWet == null) return;
        SegDry.SetResourceReference(FrameworkElement.StyleProperty,
            mode == CompareMode.Dry ? "SegmentButtonActive" : "SegmentButton");
        SegNormal.SetResourceReference(FrameworkElement.StyleProperty,
            mode == CompareMode.Normal ? "SegmentButtonActive" : "SegmentButton");
        SegWet.SetResourceReference(FrameworkElement.StyleProperty,
            mode == CompareMode.WetOnly ? "SegmentButtonActive" : "SegmentButton");
    }

    /// <summary>
    /// Update the Voice Toggle Pill button visuals for ON/OFF state.
    /// </summary>
    private void UpdateVoiceToggleVisuals(bool on)
    {
        if (BottomBar?.VoiceToggle == null || BottomBar.VoiceToggleText == null) return;

        // Find the pill border and dot inside the template
        var pill = BottomBar.VoiceToggle.Template.FindName("pill", BottomBar.VoiceToggle) as Border;
        var dot = BottomBar.VoiceToggle.Template.FindName("dot", BottomBar.VoiceToggle) as Ellipse;

        if (on)
        {
            BottomBar.VoiceToggleText.SetResourceReference(Control.ForegroundProperty, "PowerOn");
            var onColor = (FindResource("PowerOn") as SolidColorBrush)?.Color ?? Color.FromRgb(0x34, 0xD3, 0x99);
            if (pill != null)
            {
                pill.Background = new SolidColorBrush(Color.FromArgb(0x14, onColor.R, onColor.G, onColor.B));
                pill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, onColor.R, onColor.G, onColor.B));
            }
            if (dot != null) dot.Fill = new SolidColorBrush(onColor);
        }
        else
        {
            BottomBar.VoiceToggleText.SetResourceReference(Control.ForegroundProperty, "TextSub");
            var offColor = (FindResource("PowerOff") as SolidColorBrush)?.Color ?? Color.FromRgb(0xFB, 0x71, 0x85);
            var surface = (FindResource("CtrlSurface") as SolidColorBrush)?.Color ?? Color.FromRgb(0x1E, 0x1A, 0x30);
            var line = (FindResource("CtrlBorder") as SolidColorBrush)?.Color ?? Color.FromRgb(0x37, 0x31, 0x5A);
            if (pill != null)
            {
                pill.Background = new SolidColorBrush(surface);
                pill.BorderBrush = new SolidColorBrush(line);
            }
            if (dot != null) dot.Fill = new SolidColorBrush(offColor);
        }
    }

    /// <summary>Called when the update badge in the bottom bar is clicked.</summary>
    private void OnUpdateBadgeClicked()
    {
        try
        {
            if (_updateChecker == null) return;
            var url = _updateChecker.ReleaseUrl;
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            AppLog.Information("UpdateChecker: opened release page → {Url}", url);
        }
        catch (Exception ex) { AppLog.Warning(ex, "UpdateChecker: failed to open release page"); }
    }

    // ════ Toast Notifications ════

    private DispatcherTimer? _toastHideTimer;

    /// <summary>IAppServices: top toast notification (severity drives icon and color).</summary>
    public void ShowNotification(string message, string level = "info")
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ShowNotification(message, level));
            return;
        }
        if (ToastCard == null || ToastText == null || ToastIcon == null) return;

        ToastText.Text = message;
        var (glyph, color) = level switch
        {
            "success" => ("✓", Color.FromRgb(0x34, 0xD3, 0x99)),
            "warning" => ("⚠", Color.FromRgb(0xFB, 0xBF, 0x24)),
            "error"   => ("✕", Color.FromRgb(0xFB, 0x71, 0x85)),
            _         => ("ℹ", Color.FromRgb(0x8B, 0x5C, 0xF6)),
        };
        ToastIcon.Text = glyph;
        ToastIcon.Foreground = new SolidColorBrush(color);
        ToastCard.Visibility = Visibility.Visible;

        _toastHideTimer?.Stop();
        _toastHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastHideTimer.Tick += OnToastHideTick;
        _toastHideTimer.Start();
    }

    private void OnToastHideTick(object? sender, EventArgs e)
    {
        if (_toastHideTimer != null) { _toastHideTimer.Stop(); _toastHideTimer = null; }
        if (ToastCard != null) ToastCard.Visibility = Visibility.Collapsed;
    }

    private void ShowToast(string message, string level = "info") => ShowNotification(message, level);
}
