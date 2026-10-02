// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vonvert.App.UIServices;
using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.App;

/// <summary>
/// Borderless, always-on-top professional monitor for OBS window capture:
/// spectrogram waterfall (main), dry/wet waveform strip, vertical LUFS meter
/// and a pitch badge overlay. Any pane can be double-clicked to fill the
/// window; double-clicking again restores the grid. Rendering is pull-only:
/// the engine analyzers are fed on their own threads, this window just reads
/// their lock-free snapshots at ~30 fps.
/// </summary>
public partial class FullscreenVisualizationWindow : Window
{
    // Waterfall bitmap geometry: x = time (newest at right), y = frequency
    // (highest bin at top). Matches SpectrogramDisplay's default 200 frames /
    // 128 bins, so one history frame is one pixel column.
    private const int TimeCols = 200;
    private const int FreqBins = 128;

    private readonly DispatcherTimer _renderTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly WriteableBitmap _waterfall = new(TimeCols, FreqBins, 96, 96, PixelFormats.Bgra32, null);
    private readonly int[] _pixels = new int[TimeCols * FreqBins];
    private readonly PaneLayoutModel _layout = new();
    private readonly MonitorChromeModel _chrome = new();
    private readonly MonitorGuidanceModel _guidance = new();
    // Size the overlay axis was last built for; positions only move with the panel.
    private double _axisWidth = -1, _axisHeight = -1;
    // When the pipeline last saw anything above silence; drives the "no input" hint.
    private DateTime _lastPeakUtc = DateTime.MinValue;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    private readonly Polyline _pitchCurveLine = new()
    { Stroke = new SolidColorBrush(Color.FromArgb(140, 123, 93, 255)), StrokeThickness = 2.5, Stretch = Stretch.Fill };
    private bool _pitchCurveInitialized;

    private readonly Polyline _waveLine = new()
    { Stroke = new SolidColorBrush(Color.FromArgb(200, 0x3E, 0xC6, 0xFF)), StrokeThickness = 1.4 };
    private bool _showDry;

    public FullscreenVisualizationWindow()
    {
        InitializeComponent();

        WaterfallImage.Source = _waterfall;
        WaveformCanvas.Children.Add(_waveLine);
        UpdateDryWetButtons();
        UpdateChromeVisuals();

        _renderTimer.Tick += (_, _) =>
        {
            // Skip panes that are collapsed by a maximize toggle — no point
            // burning the UI thread on invisible pixels.
            if (WaterfallPanel.Visibility == Visibility.Visible) { RenderWaterfall(); RenderPitch(); }
            if (WaveformPanel.Visibility  == Visibility.Visible) RenderWaveform();
            if (LoudnessPanel.Visibility  == Visibility.Visible) RenderLufs();
            UpdateGuidance();
        };
        _renderTimer.Start();

        _idleTimer.Tick += (_, _) =>
        {
            // The help card is meant to be read, so the idle beat must not wipe the
            // cursor and toolbar out from under it; just re-arm and stay revealed.
            if (HelpPopup.IsOpen) { _idleTimer.Stop(); _idleTimer.Start(); return; }
            Cursor = Cursors.None;
            CloseHint.Opacity = 0;
            ChromeBar.Opacity = 0;
            ChromeBar.IsHitTestVisible = false;
            _idleTimer.Stop();
        };

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        // Anywhere-drag on the borderless window (handledEventsToo: the child
        // canvases mark the mouse event handled and would otherwise starve us).
        AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnWindowDragStart), handledEventsToo: true);
        MouseMove += (_, _) => ShowCursorAndHint();
        Loaded += (_, _) => MaybeRunGuide();
        // The strips scale with the window; ContentRendered is the first point where
        // ActualWidth/ActualHeight are real, and SizeChanged covers every resize after it.
        ContentRendered += (_, _) => ApplyPaneGeometry();
        SizeChanged += (_, _) => ApplyPaneGeometry();
    }

    /// <summary>
    /// Re-derive the waveform row height, the loudness column width and the banner width
    /// from the current window size. Skipped while a pane is maximized: in that state the
    /// strips are deliberately 0 or star-sized by <see cref="TogglePane"/>.
    /// </summary>
    private void ApplyPaneGeometry()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var sizes = MonitorPaneGeometry.Compute(ActualWidth, ActualHeight);
        if (_layout.Maximized is null)
        {
            WaveRow.Height = new GridLength(sizes.WaveformRowHeight);
            LufsCol.Width = new GridLength(sizes.LoudnessColumnWidth);
        }
        // The banner is top-centre, between the panel title and the pitch badges; on a
        // narrow window it has to give way instead of overlapping both.
        GuidanceBand.MaxWidth = MonitorPaneGeometry.GuidanceMaxWidth(ActualWidth);
        UpdateAxis();
    }

    /// <summary>Height the bottom of the waterfall gives up to the pitch curve and cents
    /// meter, so a label there would sit on top of them.</summary>
    private const double CurveStripHeight = 88;

    /// <summary>
    /// Build the overlay axis: hertz labels taken from the engine's own bin mapping, the
    /// band the voice actually lives in, and the time direction. Rebuilt only when the
    /// panel changes size; the two sentences are bound, so they follow the UI language
    /// without a rebuild and without anyone assigning Text.
    /// </summary>
    private void UpdateAxis()
    {
        var pipe = App.Engine?.Pipeline;
        if (pipe == null) return;
        double h = WaterfallPanel.ActualHeight, w = WaterfallPanel.ActualWidth;
        if (h <= 0 || w <= 0) return;
        if (Math.Abs(h - _axisHeight) < 0.5 && Math.Abs(w - _axisWidth) < 0.5) return;
        _axisHeight = h;
        _axisWidth = w;

        var spectro = pipe.Spectrogram;
        int bins = spectro.FrequencyBins;
        AxisCanvas.Children.Clear();

        var plate = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
        var ink = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));

        if (WaterfallAxis.BandRect(bins, spectro.GetBinFrequencyHz, h) is (double bandTop, double bandH))
        {
            var band = new Rectangle
            {
                Width = w,
                Height = bandH,
                Fill = new SolidColorBrush(Color.FromArgb(0x14, 0x8B, 0x5C, 0xF6)),
            };
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, bandTop);
            AxisCanvas.Children.Add(band);

            var bandLabel = BoundLabel(nameof(LocalizationManager.FsBandVoice),
                new SolidColorBrush(Color.FromArgb(0xCC, 0x3E, 0xC6, 0xFF)));
            Canvas.SetLeft(bandLabel, 4);
            Canvas.SetTop(bandLabel, bandTop + 2);
            AxisCanvas.Children.Add(bandLabel);
        }

        foreach (double hz in WaterfallAxis.LabelHz)
        {
            int bin = WaterfallAxis.NearestBinFor(hz, bins, spectro.GetBinFrequencyHz);
            double y = WaterfallAxis.YForBin(bin, bins, h);

            // Low frequencies land inside the pitch-curve strip; move those labels to the
            // right edge rather than dropping them or sliding them off their own row.
            bool rightSide = y > h - CurveStripHeight;

            var tick = new Rectangle
            {
                Width = 14,
                Height = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
            };
            Canvas.SetTop(tick, y);
            Canvas.SetLeft(tick, rightSide ? Math.Max(0, w - 14) : 0);
            AxisCanvas.Children.Add(tick);

            var label = new TextBlock
            {
                Text = FormatHz(hz),
                FontSize = 10,
                Foreground = ink,
                Background = plate,
                Padding = new Thickness(3, 0, 3, 0),
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetTop(label, Math.Clamp(y - 7, 0, Math.Max(0, h - 14)));
            Canvas.SetLeft(label, rightSide ? Math.Max(0, w - 14 - label.DesiredSize.Width - 2) : 16);
            AxisCanvas.Children.Add(label);
        }

        // The axis runs the wrong way for anyone used to a scrolling timeline: state it.
        var timeHint = BoundLabel(nameof(LocalizationManager.FsAxisTimeHint), ink);
        Canvas.SetRight(timeHint, 6);
        Canvas.SetBottom(timeHint, CurveStripHeight + 6);
        AxisCanvas.Children.Add(timeHint);
    }

    /// <summary>A small overlay label whose text is a bound, localizable string.</summary>
    private static TextBlock BoundLabel(string propertyName, Brush foreground)
    {
        var label = new TextBlock
        {
            FontSize = 10,
            Foreground = foreground,
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
            Padding = new Thickness(4, 1, 4, 1),
        };
        label.SetBinding(TextBlock.TextProperty, new Binding(propertyName)
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay,
        });
        return label;
    }

    /// <summary>"100 Hz" / "3 kHz" - the unit is an international symbol, so no copy here.</summary>
    private static string FormatHz(double hz)
        => hz >= 1000 ? $"{hz / 1000:0.#} kHz" : $"{hz:0} Hz";

    /// <summary>
    /// One-time walkthrough on a fresh install. Reuses the main window's spotlight control
    /// but supplies this window's copy and its OWN marker file, so finishing the monitor
    /// guide cannot mark the first-run tour as done (and vice versa).
    /// </summary>
    private void MaybeRunGuide()
    {
        try
        {
            if (!MonitorFirstRunGuide.ShouldShow(File.Exists(MonitorFirstRunGuide.MarkerPath))) return;

            var strings = LocalizationManager.Instance;
            MonitorTour.TitleText = strings.FsTourTitle;
            MonitorTour.MarkerPathOverride = MonitorFirstRunGuide.MarkerPath;
            MonitorTour.StepBodies = MonitorFirstRunGuide.StepKeys.Select(strings.GetUiString).ToArray();

            // The last stop explains the toolbar, so it has to be on screen while it is
            // being pointed at; the normal idle beat hides it again afterwards.
            ChromeBar.Opacity = 1;
            ChromeBar.IsHitTestVisible = true;

            MonitorTour.StartTour(new FrameworkElement[]
            {
                WaterfallPanel, LoudnessPanel, WaveformPanel, ChromeBar,
            });
        }
        catch (Exception ex) { AppLog.Warning(ex, "[FullscreenMonitor] first-run guide skipped"); }
    }

    // ── window chrome behaviour ─────────────────────────────────────────

    private void OnWindowDragStart(object sender, MouseButtonEventArgs e)
    {
        // While the walkthrough is up a press belongs to the tour, not to the window.
        if (MonitorTour.Visibility == Visibility.Visible) return;
        // The second click of a double-click belongs to the pane toggle: this
        // handler is registered handledEventsToo, so it must opt out by itself
        // instead of relying on the toggle's e.Handled.
        if (e.ClickCount == 2) return;
        // A press that starts on a real control belongs to that control. Without this
        // the window-level handledEventsToo hook steals it from the chrome toolbar and
        // every button click would also drag the window.
        if (IsOnInteractiveElement(e.OriginalSource)) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    /// <summary>True when the press landed on a clickable control (or on its content),
    /// i.e. anywhere the user meant to click rather than to drag the window.</summary>
    private static bool IsOnInteractiveElement(object? source)
    {
        var el = source as DependencyObject;
        while (el is not null)
        {
            if (el is ButtonBase) return true;
            el = (el is Visual || el is System.Windows.Media.Media3D.Visual3D)
                ? VisualTreeHelper.GetParent(el)
                : null;
        }
        return false;
    }

    private void ShowCursorAndHint()
    {
        Cursor = Cursors.Arrow;
        CloseHint.Opacity = 1;
        ChromeBar.Opacity = 1;
        ChromeBar.IsHitTestVisible = true;
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    /// <summary>Show the preset name overlay with a fade-in / hold / fade-out.</summary>
    public void ShowPresetName(string name)
    {
        PresetNameOverlay.Text = name;
        PresetNameOverlay.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromSeconds(0.3))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        PresetNameOverlay.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(1.5))
            { BeginTime = TimeSpan.FromSeconds(2), EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } });
    }

    // ── pane maximize (state in PaneLayoutModel; visuals applied here) ──
    // PreviewMouseLeftButtonDown so the handler still runs when a child text
    // element marks the bubbling event handled; ClickCount guards against a
    // plain click (which must keep dragging the window) and against the
    // double-toggle-cancels-itself trap of reacting to both halves of a dbl-click.

    private void WaterfallPanel_DoubleClick(object sender, MouseButtonEventArgs e) => TogglePaneOnDoubleClick(e, MonitorPane.Waterfall);
    private void WaveformPanel_DoubleClick(object sender, MouseButtonEventArgs e) => TogglePaneOnDoubleClick(e, MonitorPane.Waveform);
    private void LoudnessPanel_DoubleClick(object sender, MouseButtonEventArgs e) => TogglePaneOnDoubleClick(e, MonitorPane.Loudness);

    private void TogglePaneOnDoubleClick(MouseButtonEventArgs e, MonitorPane pane)
    {
        if (e.ClickCount != 2) return;   // first click of the pair still drags
        e.Handled = true;                // swallow the bubble → no window drag on click two
        TogglePane(pane);
    }

    private void TogglePane(MonitorPane pane)
    {
        _layout.Toggle(pane);
        var m = _layout.Maximized;

        WaterfallPanel.Visibility = (m is null || m == MonitorPane.Waterfall) ? Visibility.Visible : Visibility.Collapsed;
        WaveformPanel.Visibility  = (m is null || m == MonitorPane.Waveform)  ? Visibility.Visible : Visibility.Collapsed;
        LoudnessPanel.Visibility  = (m is null || m == MonitorPane.Loudness)  ? Visibility.Visible : Visibility.Collapsed;

        MainRow.Height = m == MonitorPane.Waveform ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        // Restoring the grid re-derives the strips from the current size (GM rules);
        // the 140 / 150 literals this replaced are what made a corner capture unreadable.
        var sizes = MonitorPaneGeometry.Compute(ActualWidth, ActualHeight);
        WaveRow.Height = m switch
        {
            MonitorPane.Waveform => new GridLength(1, GridUnitType.Star),
            MonitorPane.Waterfall or MonitorPane.Loudness => new GridLength(0),
            _ => new GridLength(sizes.WaveformRowHeight),
        };
        MainCol.Width = m == MonitorPane.Loudness ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        LufsCol.Width = m switch
        {
            MonitorPane.Loudness => new GridLength(1, GridUnitType.Star),
            MonitorPane.Waterfall or MonitorPane.Waveform => new GridLength(0),
            _ => new GridLength(sizes.LoudnessColumnWidth),
        };
    }

    // ── guidance: explain a picture that is not telling the truth ──────────

    /// <summary>
    /// DRY mode (and the power button, which maps to the same engine mode) bypasses the
    /// whole analyzer chain, so the panes freeze; and with no engine there is nothing to
    /// draw at all. Both used to look like a crash. Runs on the render tick; the state
    /// setter ignores no-op changes, so this costs one comparison per frame.
    /// </summary>
    private void UpdateGuidance()
    {
        var engine = App.Engine;
        bool ready = engine != null && engine.IsRunning;
        bool bypassed = ready && engine!.Settings.Compare == CompareMode.Dry;
        double peak = ready ? engine!.Stats.OutputLevel : 0.0;

        var now = DateTime.UtcNow;
        if (peak > MonitorGuidanceModel.SilentPeakLevel) _lastPeakUtc = now;

        var kind = _guidance.Evaluate(ready, bypassed, peak, (now - _lastPeakUtc).TotalSeconds);
        LocalizationManager.Instance.SetMonitorGuidance(kind);
    }

    // ── chrome toolbar: close / always-on-top / size cycle / reset view / help ──

    private void CloseBtn_Click(object s, RoutedEventArgs e) => Close();

    private void TopmostBtn_Click(object s, RoutedEventArgs e)
    {
        _chrome.ToggleTopmost();
        Topmost = _chrome.Topmost;
        UpdateChromeVisuals();
    }

    private void SizeBtn_Click(object s, RoutedEventArgs e)
    {
        _chrome.CycleSize();
        ApplyChromeSize();
    }

    private void ResetViewBtn_Click(object s, RoutedEventArgs e)
    {
        // "I made a mess of the layout" must have one obvious answer.
        if (_layout.Maximized is MonitorPane pane) TogglePane(pane);
        _chrome.ResetToDefault();
        Topmost = _chrome.Topmost;
        ApplyChromeSize();
        UpdateChromeVisuals();
    }

    private void HelpBtn_Click(object s, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    /// <summary>Opens the published guide for this window, in the UI language the user
    /// picked (Chinese gets the translated page, every other language the English one).</summary>
    private void GuideLink_Click(object s, RoutedEventArgs e)
        => OpenWebPage(DocsLinks.MonitorGuideUrl(LocalizationManager.Instance.Language));

    /// <summary>Best-effort hand-off to the default browser: a machine with no browser
    /// association must not lose the window the user is watching on air.</summary>
    private static void OpenWebPage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) { AppLog.Warning(ex, "[FullscreenMonitor] could not open the guide page"); }
    }

    /// <summary>Swap the panel titles between plain words and industry terms. Bound
    /// properties do the repainting; nothing here touches a Text value.</summary>
    private void LabelModeBtn_Click(object s, RoutedEventArgs e)
        => LocalizationManager.Instance.ToggleMonitorLabelMode();

    /// <summary>Resize to the selected chrome mode, clamped to the work area, then pull the
    /// window back on screen. Deliberately not <c>WindowState.Maximized</c>: a borderless
    /// WPF window maximized that way covers the taskbar, which is exactly how a user gets
    /// stuck unable to reach anything else.</summary>
    private void ApplyChromeSize()
    {
        var wa = SystemParameters.WorkArea;
        var (w, h) = _chrome.Resolve((int)wa.Width, (int)wa.Height);
        Width  = w;
        Height = h;
        Left = Math.Clamp(Left, wa.Left,  Math.Max(wa.Left,  wa.Right  - w));
        Top  = Math.Clamp(Top,  wa.Top,   Math.Max(wa.Top,   wa.Bottom - h));
    }

    /// <summary>Active state by style switch, never by assigning <c>Foreground</c> here: a
    /// local value outranks the template's own hover trigger and kills the feedback (the
    /// bug the dry/wet taps still have).</summary>
    private void UpdateChromeVisuals()
        => TopmostBtn.SetResourceReference(StyleProperty,
               _chrome.Topmost ? "SegmentButtonActive" : "SegmentButton");

    // ── waveform dry/wet source ─────────────────────────────────────────

    private void DryBtn_Click(object s, RoutedEventArgs e) { _showDry = true;  UpdateDryWetButtons(); }
    private void WetBtn_Click(object s, RoutedEventArgs e) { _showDry = false; UpdateDryWetButtons(); }

    /// <summary>
    /// Active state by style switch, never by assigning <c>Foreground</c>. A local value
    /// outranks the template's own <c>IsMouseOver</c> setter, so the old version of this
    /// method silently killed the hover feedback on both taps; it also allocated two
    /// brushes on every click. Same pattern the main window's A/B segments use.
    /// </summary>
    private void UpdateDryWetButtons()
    {
        DryBtn.SetResourceReference(StyleProperty, _showDry ? "SegmentButtonActive" : "SegmentButton");
        WetBtn.SetResourceReference(StyleProperty, _showDry ? "SegmentButton" : "SegmentButtonActive");
        // Bound read-out so the source is stated in words, not only in a tint.
        LocalizationManager.Instance.MonitorShowsDry = _showDry;
    }

    // ── renderers (pull from lock-free engine snapshots) ────────────────

    private void RenderWaterfall()
    {
        var pipe = App.Engine?.Pipeline;
        if (pipe == null) return;
        var (data, freqBins, frames, writePos) = pipe.Spectrogram.GetHistory();
        int bins = Math.Min(freqBins, FreqBins);
        int depth = data.GetLength(1);                      // ring depth from the source, not assumed

        for (int y = 0; y < FreqBins; y++)
        {
            int b = FreqBins - 1 - y;                       // top row = highest bin
            double t = (double)b / FreqBins;                // purple → cyan hue ramp
            byte br = (byte)(123 + (62 - 123) * t);
            byte bg = (byte)(93 + (198 - 93) * t);
            for (int x = 0; x < TimeCols; x++)
            {
                float v = 0f;
                int fromNewest = TimeCols - 1 - x;          // newest frame at the right edge
                if (b < bins && fromNewest < frames)
                {
                    int col = (writePos - 1 - fromNewest + 2 * depth) % depth;
                    v = data[b, col];
                }
                // Bgra32 little-endian uint layout is 0xAARRGGBB: red in bits 16-23,
                // blue in bits 0-7. The ramp is purple #7B5DFF → cyan #3EC6FF.
                _pixels[y * TimeCols + x] = unchecked((int)(0xFF000000u
                    | ((uint)(byte)(br * v) << 16) | ((uint)(byte)(bg * v) << 8) | (uint)(byte)(255 * v)));
            }
        }
        _waterfall.WritePixels(new Int32Rect(0, 0, TimeCols, FreqBins), _pixels, TimeCols * 4, 0);
    }

    private void RenderWaveform()
    {
        var pipe = App.Engine?.Pipeline;
        if (pipe == null) return;
        IWaveformCapture cap = _showDry ? pipe.InputWaveform : pipe.OutputWaveform;
        var wave = cap.GetWaveform();
        double w = WaveformCanvas.ActualWidth, h = WaveformCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const int maxPoints = 400;
        int stride = Math.Max(1, wave.Length / maxPoints);
        var pts = new PointCollection(maxPoints + 1);
        for (int i = 0; i < wave.Length; i += stride)
        {
            double x = (double)i / (wave.Length - 1) * w;
            pts.Add(new Point(x, h / 2 - wave[i] * h * 0.48));
        }
        _waveLine.Points = pts;
    }

    private void RenderLufs()
    {
        var pipe = App.Engine?.Pipeline;
        if (pipe == null) return;
        double w = LufsCanvas.ActualWidth, h = LufsCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var m = pipe.Loudness;

        LufsCanvas.Children.Clear();
        LufsCanvas.Children.Add(new Rectangle
        {
            Width = 14, Height = h, RadiusX = 7, RadiusY = 7,
            Fill = new SolidColorBrush(Color.FromArgb(40, 112, 112, 138))
        });

        double ty = LufsScale.MapToPixel(LufsScale.TargetLufs, h);
        var tick = new Rectangle { Width = w, Height = 2, Fill = new SolidColorBrush(Color.FromArgb(170, 0x39, 0xE8, 0x8A)) };
        Canvas.SetLeft(tick, 0); Canvas.SetTop(tick, ty);
        LufsCanvas.Children.Add(tick);
        var targetLabel = new TextBlock
        {
            // Drawn on a canvas, so it is not covered by the XAML alpha guard: the target
            // line is the one number a podcast user is supposed to aim at, it has to survive
            // an OBS downscale.
            Text = "-23", FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(210, 0x39, 0xE8, 0x8A))
        };
        Canvas.SetLeft(targetLabel, 18); Canvas.SetTop(targetLabel, ty - 7);
        LufsCanvas.Children.Add(targetLabel);

        AddLufsPointer(m.MomentaryLufs, h, Color.FromRgb(0x3E, 0xC6, 0xFF));
        AddLufsPointer(m.ShortTermLufs, h, Color.FromRgb(0x8B, 0x5C, 0xF6));
        AddLufsPointer(m.IntegratedLufs, h, Color.FromRgb(0xFF, 0xD7, 0x00));

        LufsTruePeakText.Text = $"TP {m.TruePeakDb:F1} dBTP";
        LufsIntegratedText.Text = $"{LocalizationManager.Instance.FsIntegrated} {m.IntegratedLufs:F1} LUFS";
    }

    private void AddLufsPointer(float lufs, double h, Color color)
    {
        double y = LufsScale.MapToPixel(lufs, h);
        var p = new Rectangle { Width = 34, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(p, 0); Canvas.SetTop(p, y - 1.5);
        LufsCanvas.Children.Add(p);
    }

    private void ResetIntegrated_Click(object s, RoutedEventArgs e)
        => App.Engine?.Pipeline.Loudness.RequestReset();   // applied on the analyzer thread

    // ── pitch overlay ───────────────────────────────────────────────────

    private void RenderPitch()
    {
        var pipe = App.Engine?.Pipeline;
        if (pipe == null) return;
        var snap = pipe.Pitch.GetSnapshot();
        bool voiced = snap.IsVoiced && snap.Frequency > 1f;

        FsPitchNoteBadge.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
        FsPitchCentsBadge.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
        if (voiced)
        {
            float abs = MathF.Abs(snap.CentsDeviation);
            var noteColor = abs < 10f ? Color.FromRgb(0x39, 0xE8, 0x8A)
                          : abs < 25f ? Color.FromRgb(0xFF, 0xD7, 0x00)
                          : Color.FromRgb(0xFF, 0x6B, 0x6B);
            FsPitchNoteText.Text = snap.DisplayNote;
            FsPitchNoteText.Foreground = new SolidColorBrush(noteColor);
            FsPitchCentsText.Text = $"{snap.DisplayCents} {LocalizationManager.Instance.CentsUnit}";
            FsPitchCentsText.Foreground = new SolidColorBrush(Color.FromArgb(187, noteColor.R, noteColor.G, noteColor.B));
            FsPitchFreqText.Text = $"{snap.Frequency:F0} Hz";
        }
        else FsPitchFreqText.Text = "";

        if (voiced) RenderCentsMeter(snap.CentsDeviation);
        FsCentsMeterCanvas.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
        RenderPitchCurve(pipe);
    }

    private void RenderCentsMeter(float cents)
    {
        double w = FsCentsMeterCanvas.ActualWidth;
        if (w <= 0) return;
        FsCentsMeterCanvas.Children.Clear();
        FsCentsMeterCanvas.Children.Add(new Rectangle
        {
            Width = w, Height = 8, RadiusX = 4, RadiusY = 4,
            Fill = new SolidColorBrush(Color.FromArgb(40, 112, 112, 138))
        });
        double cx = w / 2;
        var center = new Rectangle { Width = 3, Height = 12, Fill = new SolidColorBrush(Color.FromArgb(80, 112, 112, 138)) };
        Canvas.SetLeft(center, cx - 1.5); Canvas.SetTop(center, -2);
        FsCentsMeterCanvas.Children.Add(center);

        float abs = MathF.Abs(cents);
        var dotColor = abs < 10f ? Color.FromRgb(0x39, 0xE8, 0x8A)
                     : abs < 25f ? Color.FromRgb(0xFF, 0xD7, 0x00)
                     : Color.FromRgb(0xFF, 0x6B, 0x6B);
        var dot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(dotColor) };
        Canvas.SetLeft(dot, Math.Clamp((cents + 50f) / 100f, 0, 1) * w - 5);
        Canvas.SetTop(dot, -1);
        FsCentsMeterCanvas.Children.Add(dot);
    }

    private void RenderPitchCurve(IAudioProcessor pipe)
    {
        double w = PitchCurveCanvas.ActualWidth, h = PitchCurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var history = pipe.Pitch.GetHistory();
        int count = pipe.Pitch.GetHistoryCount();
        if (count < 2) return;
        if (!_pitchCurveInitialized)
        {
            PitchCurveCanvas.Children.Add(_pitchCurveLine);
            _pitchCurveInitialized = true;
        }
        var pts = new PointCollection();
        int startIdx = (history.Length - count + history.Length) % history.Length;
        for (int i = 0; i < count; i++)
        {
            float val = history[(startIdx + i) % history.Length];
            if (val < 0f) continue;
            pts.Add(new Point((double)i / (history.Length - 1) * w, h - val * h));
        }
        _pitchCurveLine.Points = pts;
    }

    protected override void OnClosed(EventArgs e)
    {
        _renderTimer.Stop();
        _idleTimer.Stop();
        // Release visual references (brushes can hold engine snapshots) before teardown.
        try { WaterfallImage.Source = null; } catch { /* best-effort */ }
        try { PitchCurveCanvas.Children.Clear(); } catch { /* best-effort */ }
        try { FsCentsMeterCanvas.Children.Clear(); } catch { /* best-effort */ }
        try { WaveformCanvas.Children.Clear(); } catch { /* best-effort */ }
        try { LufsCanvas.Children.Clear(); } catch { /* best-effort */ }
        base.OnClosed(e);
    }
}
