// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>Resolved pixel sizes for the monitor's regions at one window size.</summary>
public readonly record struct MonitorPaneSizes(
    int WaveformRowHeight, int LoudnessColumnWidth, int MainWidth, int MainHeight);

/// <summary>
/// Proportional geometry for the monitor's two fixed strips.
///
/// They used to be 140 px and 150 px regardless of window size, so the common
/// "capture it small into a corner" layout squeezed the main view to 490x220 at the
/// minimum size - the one picture the window exists for. Scaling both strips keeps the
/// main view's share constant, and the ratios are taken from the shipped 1280x720
/// design so that default look is reproduced exactly (GM-001).
/// </summary>
public static class MonitorPaneGeometry
{
    /// <summary>140 / 720 and 150 / 1280 - the shipped defaults, expressed as ratios.</summary>
    public const double WaveformRatio = 140.0 / 720.0;
    public const double LoudnessRatio = 150.0 / 1280.0;

    // Floor: below this a strip stops carrying information. Ceiling: above it the strips
    // start stealing height from the main view on large windows.
    public const int MinWaveformHeight = 90;
    public const int MaxWaveformHeight = 220;
    public const int MinLoudnessWidth = 110;
    public const int MaxLoudnessWidth = 240;

    /// <summary>The main view never collapses below these, however odd the window size is.</summary>
    public const int MinMainWidth = 240;
    public const int MinMainHeight = 160;

    public const double GuidanceRatio = 0.55;
    public const int MinGuidanceWidth = 240;
    public const int MaxGuidanceWidth = 620;

    public static int WaveformHeight(double height, double fontFactor = 1.0)
        => ClampRound(height * WaveformRatio * fontFactor,
                      Scaled(MinWaveformHeight, fontFactor), Scaled(MaxWaveformHeight, fontFactor));

    public static int LoudnessWidth(double width, double fontFactor = 1.0)
        => ClampRound(width * LoudnessRatio * fontFactor,
                      Scaled(MinLoudnessWidth, fontFactor), Scaled(MaxLoudnessWidth, fontFactor));

    /// <summary>
    /// Width the guidance banner may claim. It is centred at the top, between the panel
    /// title on the left and the pitch badges on the right, so on a narrow window it has
    /// to give way rather than overlap both.
    /// </summary>
    public static int GuidanceMaxWidth(double width)
        => ClampRound(width * GuidanceRatio, MinGuidanceWidth, MaxGuidanceWidth);

    /// <summary>
    /// Strips sized for one window at one text size. fontFactor comes from
    /// MonitorFontSizes.Factor: bigger text needs a wider column and a taller strip, or the
    /// labels clip - the 1280x720 capture at the large size showed "Target -23 LUFS (Broadcast)"
    /// cut off mid-word. At factor 1.0 the arithmetic is exactly the shipped one, so the default
    /// look is untouched (GM-001).
    /// </summary>
    public static MonitorPaneSizes Compute(double width, double height, double fontFactor = 1.0)
    {
        int wave = WaveformHeight(height, fontFactor);
        int lufs = LoudnessWidth(width, fontFactor);

        // A non-positive window means the layout pass ran before the screen was known;
        // report the floors instead of a zero or negative region that GridLength rejects.
        int mainW = width <= 0 ? MinMainWidth
                               : Math.Max((int)Math.Round(width) - lufs, MinMainWidth);
        int mainH = height <= 0 ? MinMainHeight
                                : Math.Max((int)Math.Round(height) - wave, MinMainHeight);
        return new MonitorPaneSizes(wave, lufs, mainW, mainH);
    }

    private static int Scaled(int pixels, double fontFactor) =>
        (int)Math.Round(pixels * Math.Max(0.5, fontFactor), MidpointRounding.AwayFromZero);

    private static int ClampRound(double value, int min, int max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) value = min;
        return (int)Math.Round(Math.Clamp(value, min, max), MidpointRounding.AwayFromZero);
    }
}
