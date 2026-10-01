// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.Engine.Diagnostics;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  SpectrogramDisplay — SG-001 ~ SG-005
// ═══════════════════════════════════════════════════════════════════

public sealed class SpectrogramDisplayTests
{
    private const int Rate = 48000;

    private static float[] Sine(float hz, int n, float amp = 0.8f)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = amp * MathF.Sin(2f * MathF.PI * hz * i / Rate);
        return s;
    }

    [Fact(DisplayName = "SG-001: a 1 kHz tone puts the column peak near 1 kHz")]
    public void SG001_SinePeak_BinMatchesFrequency()
    {
        var sg = new SpectrogramDisplay(fftSize: 2048, frequencyBins: 128, historyDepth: 200);
        sg.FeedSamples(Sine(1000f, 4800));

        var col = sg.GetCurrentColumn();
        int peak = 0;
        for (int b = 1; b < col.Length; b++) if (col[b] > col[peak]) peak = b;

        float peakHz = sg.GetBinFrequencyHz(peak);
        Assert.InRange(peakHz, 700f, 1400f);
        Assert.True(col[peak] > 0.5f, $"peak bin energy too low: {col[peak]:F3}");
    }

    [Fact(DisplayName = "SG-002: bin frequency axis never decreases; mid/high bins resolve strictly")]
    public void SG002_FrequencyAxisMonotonic()
    {
        var sg = new SpectrogramDisplay();
        float prev = -1f;
        for (int b = 0; b < sg.FrequencyBins; b++)
        {
            float hz = sg.GetBinFrequencyHz(b);
            Assert.True(hz >= prev, $"bin {b}: {hz} < {prev} — axis must never decrease");
            prev = hz;
        }
        // The quadratic spacing plus int truncation collapses the first few low
        // bins onto 0 Hz (documented source behaviour); from the mid-range on,
        // every bin must resolve a distinct, strictly rising frequency, and the
        // top bin must approach Nyquist.
        for (int b = sg.FrequencyBins / 2; b < sg.FrequencyBins; b++)
            Assert.True(sg.GetBinFrequencyHz(b) > sg.GetBinFrequencyHz(b - 1), $"bin {b} must resolve above its neighbour");
        Assert.True(sg.GetBinFrequencyHz(sg.FrequencyBins - 1) > 15000f, "top bin should approach Nyquist");
    }

    [Fact(DisplayName = "SG-003: history fills over frames and wraps past depth")]
    public void SG003_HistoryRing()
    {
        var sg = new SpectrogramDisplay(fftSize: 512, frequencyBins: 32, historyDepth: 8);
        sg.FeedSamples(Sine(500f, 512 * 30));            // > 8 hops of 256
        var (data, freqBins, frames, writePos) = sg.GetHistory();
        Assert.Equal(8, frames);                          // clamped at depth
        Assert.InRange(writePos, 0, 7);
        Assert.Equal(32, freqBins);
        Assert.NotNull(data);
    }

    [Fact(DisplayName = "SG-004: Reset clears column, history and ring state")]
    public void SG004_Reset()
    {
        var sg = new SpectrogramDisplay(fftSize: 512, frequencyBins: 32, historyDepth: 8);
        sg.FeedSamples(Sine(800f, 512 * 10));
        sg.Reset();
        Assert.Equal(0, sg.TimeFrames);
        Assert.All(sg.GetCurrentColumn().ToArray(), v => Assert.Equal(0f, v));
    }

    [Fact(DisplayName = "SG-005: no availability gate — feeding always produces energy")]
    public void SG005_NoLicenseGateRegression()
    {
        // Regression guard: the OSS source carried a license gate that silently
        // swallowed FeedSamples. The ported class must have no such state.
        var sg = new SpectrogramDisplay();
        sg.FeedSamples(Sine(1000f, 4800));
        bool anyEnergy = false;
        foreach (var v in sg.GetCurrentColumn()) if (v > 0.1f) { anyEnergy = true; break; }
        Assert.True(anyEnergy);
        Assert.True(sg.TimeFrames > 0);
    }
}
