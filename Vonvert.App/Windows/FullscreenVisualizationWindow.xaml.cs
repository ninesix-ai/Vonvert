// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vonvert.App.UIServices;
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

        _renderTimer.Tick += (_, _) =>
        {
            // Skip panes that are collapsed by a maximize toggle — no point
            // burning the UI thread on invisible pixels.
            if (WaterfallPanel.Visibility == Visibility.Visible) { RenderWaterfall(); RenderPitch(); }
            if (WaveformPanel.Visibility  == Visibility.Visible) RenderWaveform();
            if (LoudnessPanel.Visibility  == Visibility.Visible) RenderLufs();
        };
        _renderTimer.Start();

        _idleTimer.Tick += (_, _) => { Cursor = Cursors.None; CloseHint.Opacity = 0; _idleTimer.Stop(); };

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        // Anywhere-drag on the borderless window (handledEventsToo: the child
        // canvases mark the mouse event handled and would otherwise starve us).
        AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnWindowDragStart), handledEventsToo: true);
        MouseMove += (_, _) => ShowCursorAndHint();
    }

    // ── window chrome behaviour ─────────────────────────────────────────

    private void OnWindowDragStart(object sender, MouseButtonEventArgs e)
    {
        // The second click of a double-click belongs to the pane toggle: this
        // handler is registered handledEventsToo, so it must opt out by itself
        // instead of relying on the toggle's e.Handled.
        if (e.ClickCount == 2) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void ShowCursorAndHint()
    {
        Cursor = Cursors.Arrow;
        CloseHint.Opacity = 1;
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
        WaveRow.Height = m switch
        {
            MonitorPane.Waveform => new GridLength(1, GridUnitType.Star),
            MonitorPane.Waterfall or MonitorPane.Loudness => new GridLength(0),
            _ => new GridLength(140),
        };
        MainCol.Width = m == MonitorPane.Loudness ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        LufsCol.Width = m switch
        {
            MonitorPane.Loudness => new GridLength(1, GridUnitType.Star),
            MonitorPane.Waterfall or MonitorPane.Waveform => new GridLength(0),
            _ => new GridLength(150),
        };
    }

    // ── waveform dry/wet source ─────────────────────────────────────────

    private void DryBtn_Click(object s, RoutedEventArgs e) { _showDry = true;  UpdateDryWetButtons(); }
    private void WetBtn_Click(object s, RoutedEventArgs e) { _showDry = false; UpdateDryWetButtons(); }

    private void UpdateDryWetButtons()
    {
        var on  = new SolidColorBrush(Color.FromRgb(0x3E, 0xC6, 0xFF));
        var off = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
        DryBtn.Foreground = _showDry ? on : off;
        WetBtn.Foreground = _showDry ? off : on;
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
        var tick = new Rectangle { Width = w, Height = 2, Fill = new SolidColorBrush(Color.FromArgb(120, 0x39, 0xE8, 0x8A)) };
        Canvas.SetLeft(tick, 0); Canvas.SetTop(tick, ty);
        LufsCanvas.Children.Add(tick);
        var targetLabel = new TextBlock
        {
            Text = "-23", FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 0x39, 0xE8, 0x8A))
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
