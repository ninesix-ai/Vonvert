// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>
/// Maps the spectrogram's frequency bins to screen positions for the axis labels and the
/// voice band drawn over the waterfall.
///
/// The frequency-to-Hz lookup is injected rather than reimplemented: the engine owns the
/// bin mapping (quadratic, log-like), and a second copy here would eventually disagree
/// with the pixels it is labelling - which is worse than having no labels at all.
/// </summary>
public static class WaterfallAxis
{
    /// <summary>Targets labelled on the frequency axis, lowest first.</summary>
    public static readonly double[] LabelHz = { 100, 300, 1000, 3000, 8000 };

    /// <summary>Band where voice fundamentals and intelligibility live (200 - 3500 Hz).</summary>
    public const double BandLowHz = 200;
    public const double BandHighHz = 3500;

    /// <summary>
    /// Bin whose centre frequency is closest to <paramref name="hz"/>. Requests outside
    /// the visible range clamp to the first or last bin instead of falling off the panel.
    /// </summary>
    public static int NearestBinFor(double hz, int bins, Func<int, float> binHz)
    {
        if (bins <= 0) throw new ArgumentOutOfRangeException(nameof(bins), "the spectrogram has no bins");
        if (double.IsNaN(hz)) return 0;

        int best = 0;
        double bestError = double.MaxValue;
        for (int b = 0; b < bins; b++)
        {
            double error = Math.Abs(binHz(b) - hz);
            if (error < bestError)
            {
                bestError = error;
                best = b;
            }
        }
        return best;
    }

    /// <summary>
    /// Screen y of a bin: the highest bin sits at the top edge, bin 0 at the bottom, so a
    /// label lines up with the row of pixels it describes.
    /// </summary>
    public static double YForBin(int bin, int bins, double height)
    {
        if (bins <= 1) return 0;
        int clamped = Math.Clamp(bin, 0, bins - 1);
        double fromTop = bins - 1 - clamped;
        return fromTop / (bins - 1) * height;
    }

    /// <summary>Top and bottom y of the shaded voice band, or null when it cannot be shown.</summary>
    public static (double TopY, double Height)? BandRect(int bins, Func<int, float> binHz, double height)
    {
        if (bins <= 1 || height <= 0) return null;

        int low = NearestBinFor(BandLowHz, bins, binHz);
        int high = NearestBinFor(BandHighHz, bins, binHz);
        if (high <= low) return null;                        // band collapsed on a tiny panel

        double yHigh = YForBin(high, bins, height);         // higher frequency is higher up
        double yLow = YForBin(low, bins, height);
        return (yHigh, yLow - yHigh);
    }
}
