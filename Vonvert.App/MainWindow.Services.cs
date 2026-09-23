// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows.Shapes;
using Vonvert.App.Services;

namespace Vonvert.App;

public partial class MainWindow
{
    // ════ Spectrum Visualization (delegated to SpectrumVisualizer) ════

    private void InitSpectrumVisualizer()
    {
        if (SpectrumCanvas == null || App.Engine == null) return;
        _spectrumViz = new SpectrumVisualizer();
        _spectrumViz.Initialize(SpectrumCanvas, App.Engine);
    }

    private void RenderSpectrum()
    {
        _spectrumViz?.Render(SpectrumCanvas, App.Engine);
    }

    // ════ Pitch Visualization ════

    private void RenderPitch()
    {
        if (App.Engine == null) return;

        var snap = App.Engine.Pipeline.Pitch.GetSnapshot();
        bool voiced = snap.IsVoiced && snap.Frequency > 1f;

        // Update note badge
        if (PitchNoteBadge != null)
        {
            PitchNoteBadge.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
            if (voiced)
            {
                PitchNoteText.Text = snap.DisplayNote;

                // Color code: green when close to target, yellow/orange when off
                float absCents = MathF.Abs(snap.CentsDeviation);
                if (absCents < 10f)
                    PitchNoteText.Foreground = new SolidColorBrush(Color.FromRgb(0x39, 0xE8, 0x8A)); // green
                else if (absCents < 25f)
                    PitchNoteText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // gold
                else
                    PitchNoteText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)); // red
            }
        }

        // Update cents badge
        if (PitchCentsBadge != null)
        {
            PitchCentsBadge.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
            if (voiced)
            {
                PitchCentsText.Text = $"{snap.DisplayCents} {L.CentsUnit}";

                float absCents = MathF.Abs(snap.CentsDeviation);
                byte alpha = 170;
                Color centsColor = absCents < 10f
                    ? Color.FromArgb(alpha, 0x39, 0xE8, 0x8A)  // green
                    : absCents < 25f
                    ? Color.FromArgb(alpha, 0xFF, 0xD7, 0x00)  // gold
                    : Color.FromArgb(alpha, 0xFF, 0x6B, 0x6B); // red
                PitchCentsText.Foreground = new SolidColorBrush(centsColor);
            }
        }

        // Update frequency text
        if (PitchFreqText != null)
        {
            PitchFreqText.Text = voiced ? $"{snap.Frequency:F0} {L.HzUnit}" : "";
        }

        // Update cents meter
        if (CentsMeterCanvas != null)
        {
            CentsMeterCanvas.Visibility = voiced ? Visibility.Visible : Visibility.Collapsed;
            if (voiced)
            {
                RenderCentsMeter(snap.CentsDeviation);
            }
        }

        // Update pitch curve
        if (PitchCurveCanvas != null)
        {
            RenderPitchCurve();
        }
    }

    private void RenderCentsMeter(float cents)
    {
        // Horizontal bar: center = 0 cents, left = -50, right = +50
        double w = CentsMeterCanvas.ActualWidth;
        if (w <= 0) return;

        CentsMeterCanvas.Children.Clear();

        // Background track
        var track = new Rectangle
        {
            Width = w,
            Height = 4,
            RadiusX = 2,
            RadiusY = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 112, 112, 138))
        };
        CentsMeterCanvas.Children.Add(track);

        // Center line marker
        double centerX = w / 2;
        var centerLine = new Rectangle
        {
            Width = 2,
            Height = 6,
            Fill = new SolidColorBrush(Color.FromArgb(60, 112, 112, 138))
        };
        Canvas.SetLeft(centerLine, centerX - 1);
        Canvas.SetTop(centerLine, -1);
        CentsMeterCanvas.Children.Add(centerLine);

        // Cents indicator dot
        double normalizedCents = (cents + 50f) / 100f; // map -50..+50 to 0..1
        normalizedCents = Math.Clamp(normalizedCents, 0, 1);
        double dotX = normalizedCents * w - 3;

        float absCents = MathF.Abs(cents);
        Color dotColor = absCents < 10f
            ? Color.FromRgb(0x39, 0xE8, 0x8A)  // green
            : absCents < 25f
            ? Color.FromRgb(0xFF, 0xD7, 0x00)  // gold
            : Color.FromRgb(0xFF, 0x6B, 0x6B); // red

        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(dotColor)
        };
        Canvas.SetLeft(dot, dotX);
        Canvas.SetTop(dot, -1);
        CentsMeterCanvas.Children.Add(dot);
    }

    private void RenderPitchCurve()
    {
        double w = PitchCurveCanvas.ActualWidth;
        double h = PitchCurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var history = App.Engine.Pipeline.Pitch.GetHistory();
        int count = App.Engine.Pipeline.Pitch.GetHistoryCount();
        if (count < 2) return;

        // Initialize polyline once
        if (!_pitchCurveInitialized)
        {
            PitchCurveCanvas.Children.Add(_pitchCurveLine);
            _pitchCurveInitialized = true;
        }

        var points = new PointCollection();
        int startIdx = (history.Length - count + history.Length) % history.Length;

        for (int i = 0; i < count; i++)
        {
            int idx = (startIdx + i) % history.Length;
            float val = history[idx];

            double x = (double)i / (history.Length - 1) * w;

            if (val < 0f)
            {
                // Silence — break the line by not adding a point
                continue;
            }

            double y = h - val * h;
            points.Add(new Point(x, y));
        }

        _pitchCurveLine.Points = points;
    }

    // ════ Device Helpers ════

    /// <summary>Show/hide the no-mic banner based on currently enumerated inputs.</summary>
    private void UpdateMicBanner()
    {
        try
        {
            bool hasInput = App.Devices != null && App.Devices.InputDevices().Count > 0;
            if (NoMicBanner != null)
                NoMicBanner.Visibility = hasInput ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex) { AppLog.Warning(ex, "UpdateMicBanner failed"); }
    }

    /// <summary>
    /// Virtual-cable check. Shows VB-Cable presence in the bottom bar and in
    /// the Settings tab so the user knows whether the virtual mic is ready.
    /// </summary>
    private void CheckVBCable()
    {
        try
        {
            var vb = App.Devices?.FindVBCable();
            bool detected = vb != null;

            var brush = detected
                ? TryFindResource("PowerOn") as Brush ?? Brushes.Gray
                : TryFindResource("Warning") as Brush ?? Brushes.Gray;

            if (BottomBar?.VbCableStatus != null)
            {
                // Cable present → show a plain, non-interactive status label.
                BottomBar.VbCableStatus.Text = L.VbCableDetected;
                BottomBar.VbCableStatus.Foreground = brush;
                BottomBar.VbCableStatus.Visibility = detected ? Visibility.Visible : Visibility.Collapsed;
            }

            if (BottomBar?.VbCableLink != null && BottomBar.VbCableLinkRun != null)
            {
                // Cable missing → show a clickable install link that opens the download page.
                BottomBar.VbCableLinkRun.Text = L.InstallVbCableLink;
                if (BottomBar.VbCableHyperlink != null) BottomBar.VbCableHyperlink.Foreground = brush;
                BottomBar.VbCableLink.Visibility = detected ? Visibility.Collapsed : Visibility.Visible;
            }

            if (VbStatusText != null)
            {
                VbStatusText.Text = detected ? L.VbCableDetected : L.InstallVbCable;
                VbStatusText.Foreground = brush;
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "CheckVBCable failed"); }
    }

    /// <summary>Manual device re-enumeration (button inside the no-mic banner).</summary>
    private void RefreshDevices_Click(object s, RoutedEventArgs e)
    {
        try
        {
            UpdateMicBanner();
            CheckVBCable();
        }
        catch { /* manual device refresh is best-effort */ }
    }
}
