// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.App.Services;

/// <summary>
/// Encapsulates spectrum analyzer visualization: bar creation, per-frame rendering,
/// peak-hold with decay, and purple→cyan gradient coloring.
/// Extracted from MainWindow.Services.cs to isolate rendering state from window logic.
/// </summary>
public sealed class SpectrumVisualizer
{
    private Rectangle[]? _bars;
    private Rectangle[]? _peaks;
    private readonly float[] _peakHold = new float[32];
    private readonly float[] _peakDecay = new float[32];

    /// <summary>Number of spectrum bars (determined by the engine's spectrum analyzer).</summary>
    public int BarCount => _bars?.Length ?? 0;

    /// <summary>
    /// Pre-create rectangle elements on the canvas, sized to match the engine's spectrum bins.
    /// Call once after the engine is initialized.
    /// </summary>
    public void Initialize(Canvas canvas, VoiceEngine engine)
    {
        if (canvas == null || engine == null) return;

        var spectrum = engine.Pipeline.Spectrum.GetSpectrumBars();
        int barCount = spectrum.Length;
        _bars = new Rectangle[barCount];
        _peaks = new Rectangle[barCount];

        for (int i = 0; i < barCount; i++)
        {
            _bars[i] = new Rectangle { RadiusX = 1, RadiusY = 1 };
            _peaks[i] = new Rectangle { RadiusX = 1, RadiusY = 1, Height = 2 };
            canvas.Children.Add(_bars[i]);
            canvas.Children.Add(_peaks[i]);
        }
    }

    /// <summary>
    /// Render one frame of the spectrum visualization.
    /// Call from the 80 ms metrics timer tick.
    /// </summary>
    public void Render(Canvas canvas, VoiceEngine engine)
    {
        if (_bars == null || canvas == null || engine == null) return;

        var spectrum = engine.Pipeline.Spectrum.GetSpectrumBars();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int barCount = _bars.Length;
        double gap = 2;
        double barWidth = (w - gap * (barCount + 1)) / barCount;
        if (barWidth < 1) barWidth = 1;

        for (int i = 0; i < barCount; i++)
        {
            float val = i < spectrum.Length ? spectrum[i] : 0f;
            double barHeight = val * h * 0.9;
            double x = gap + i * (barWidth + gap);
            double y = h - barHeight;

            // Color: purple → cyan gradient across bars
            double t = (double)i / barCount;
            byte r = (byte)(123 + (62 - 123) * t);
            byte g = (byte)(93 + (198 - 93) * t);
            byte b = 255;
            var color = Color.FromRgb(
                (byte)Math.Clamp((int)r, 0, 255),
                (byte)Math.Clamp((int)g, 0, 255),
                (byte)Math.Clamp((int)b, 0, 255));

            // Main bar
            var bar = _bars[i];
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            bar.Width = barWidth;
            bar.Height = Math.Max(0, barHeight);
            bar.Fill = new SolidColorBrush(Color.FromArgb(
                (byte)(160 + 95 * val), color.R, color.G, color.B));

            // Peak hold
            if (val > _peakHold[i])
            {
                _peakHold[i] = val;
                _peakDecay[i] = 0f;
            }
            else
            {
                _peakDecay[i] += 0.012f;
                _peakHold[i] = Math.Max(0, _peakHold[i] - _peakDecay[i]);
            }

            double peakY = h - _peakHold[i] * h * 0.9;
            var peak = _peaks![i];
            Canvas.SetLeft(peak, x);
            Canvas.SetTop(peak, peakY - 1);
            peak.Width = barWidth;
            peak.Fill = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B));
        }
    }
}
