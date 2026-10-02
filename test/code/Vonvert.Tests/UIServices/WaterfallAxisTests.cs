// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.App.UIServices;
using Vonvert.Engine.Diagnostics;

namespace Vonvert.Tests.UIServices;

// AX-001 ~ AX-007: the axis labels drawn over the waterfall.
//
// The picture used to arrive with no numbers at all, while its frequency axis is
// quadratic - so the lower third carried the voice and the upper two thirds carried hiss
// and noise, which reads backwards to everyone who has not used a spectrogram before.
// These tests keep the labels pinned to the engine's own bin mapping rather than to a
// copy of it.
public sealed class WaterfallAxisTests
{
    private const int Bins = 128;

    // The real engine object, not a reimplementation: the labels must agree with the
    // pixels, and only the engine knows how bins map to hertz.
    private static readonly SpectrogramDisplay Engine = new(fftSize: 2048, frequencyBins: Bins, historyDepth: 200);

    private static float Hz(int bin) => Engine.GetBinFrequencyHz(bin);

    [Fact(DisplayName = "AX-001: label bins ascend with frequency, matching the axis direction")]
    public void AX001_BinsAscend()
    {
        int prev = -1;
        foreach (double hz in WaterfallAxis.LabelHz)
        {
            int bin = WaterfallAxis.NearestBinFor(hz, Bins, Hz);
            Assert.True(bin > prev, $"{hz} Hz landed on bin {bin}, not above {prev}");
            prev = bin;
        }
    }

    [Fact(DisplayName = "AX-002: every label sits within a bin or two of the frequency it claims")]
    public void AX002_LabelsAreAccurate()
    {
        foreach (double hz in new[] { 100.0, 300.0, 1000.0, 3000.0, 8000.0 })
        {
            int bin = WaterfallAxis.NearestBinFor(hz, Bins, Hz);
            double actual = Hz(bin);
            // The quadratic axis is fine near the bottom and coarse at the top; 12% keeps
            // the test honest about both instead of asserting a precision that is not there.
            Assert.True(Math.Abs(actual - hz) / hz <= 0.12,
                $"label {hz} Hz resolved to {actual:F0} Hz (bin {bin}) - more than 12% off");
        }
    }

    [Fact(DisplayName = "AX-003: y positions run top-to-bottom with frequency and stay inside the panel")]
    public void AX003_GeometryWithinPanel()
    {
        const double height = 600;
        Assert.Equal(0, WaterfallAxis.YForBin(Bins - 1, Bins, height), 3);      // highest bin at the top edge
        Assert.Equal(height, WaterfallAxis.YForBin(0, Bins, height), 3);        // bin 0 at the bottom
        Assert.Equal(height, WaterfallAxis.YForBin(-5, Bins, height), 3);       // clamped
        Assert.Equal(0, WaterfallAxis.YForBin(9999, Bins, height), 3);
        for (int b = 0; b < Bins; b++)
        {
            double y = WaterfallAxis.YForBin(b, Bins, height);
            Assert.InRange(y, 0, height);
        }
    }

    [Fact(DisplayName = "AX-004: requests beyond the visible range clamp instead of falling off the panel")]
    public void AX004_OutOfRangeClamps()
    {
        Assert.Equal(Bins - 1, WaterfallAxis.NearestBinFor(96000, Bins, Hz));   // above Nyquist
        Assert.Equal(0, WaterfallAxis.NearestBinFor(-500, Bins, Hz));           // below the axis
        Assert.Equal(0, WaterfallAxis.NearestBinFor(double.NaN, Bins, Hz));
    }

    [Fact(DisplayName = "AX-005: the voice band is drawn, inside the panel, and covers less than half the height")]
    public void AX005_BandIsWhereTheDocClaims()
    {
        var rect = WaterfallAxis.BandRect(Bins, Hz, 600);
        Assert.NotNull(rect);
        var (top, height) = rect!.Value;
        Assert.True(top >= 0 && height > 0 && top + height <= 600.5,
            $"band is y {top}..{top + height} of 600");
        // This pins the claim made in the guide: fundamentals and clarity sit in the lower
        // part of the picture, so the shaded band must not be most of it.
        Assert.True(height < 600 * 0.5, $"voice band covers {height / 600:P0} of the panel");
    }

    [Fact(DisplayName = "AX-006: a panel too small to place the band returns null rather than a negative rectangle")]
    public void AX006_BandDegradesCleanly()
    {
        Assert.Null(WaterfallAxis.BandRect(Bins, Hz, 0));
        Assert.Null(WaterfallAxis.BandRect(1, Hz, 600));
    }

    [Fact(DisplayName = "AX-007: a bad bin count is a programming error, not a silent zero")]
    public void AX007_RejectsEmptyAxis()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaterfallAxis.NearestBinFor(1000, 0, Hz));
    }
}
