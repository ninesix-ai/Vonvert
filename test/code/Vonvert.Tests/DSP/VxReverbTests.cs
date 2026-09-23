// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guard for the reverb delay-line bug.
// Freeverb tunes its 8 comb filters to DISTINCT delay lengths (1116..1617).
// The bug read every comb at the full buffer capacity (2048), so all combs
// resonated identically (metallic) and the first wet echo was pushed to sample
// 2048. With correct taps the comb echoes land at 1116..1617 — inside the
// [1100,1650] analysis window, which a capacity-collapsed comb leaves silent.

namespace Vonvert.Tests.DSP;

using Vonvert.Engine.DspEngine.TimeBased;
using Xunit;

public class ReverbDecorrelationTests
{
    [Fact(DisplayName = "RV-DECORR: comb echoes arrive before the 2048-sample buffer wrap")]
    public void CombDelays_AreDistinct_NotCollapsedToCapacity()
    {
        var reverb = new VxReverb { Wet = 1f };   // wet-only — the dry path contributes nothing
        const int len = 2000;                     // < 2048, so a capacity-collapsed comb cannot echo yet
        var buf = new float[len];
        buf[0] = 1f;                              // unit impulse at sample 0
        reverb.Process(buf.AsSpan());

        // min(CombTaps)=1116 .. max(CombTaps)=1617 all fall inside [1100,1650].
        // Reading every comb at the 2048 capacity leaves this whole window silent.
        float windowPeak = 0f;
        for (int i = 1100; i <= 1650; i++)
            windowPeak = MathF.Max(windowPeak, MathF.Abs(buf[i]));

        Assert.True(windowPeak > 1e-4f,
            $"No wet energy in [1100,1650] (peak={windowPeak:E3}); comb delays collapsed to the " +
            "2048 buffer capacity — per-comb decorrelation lost (H2 regression).");
    }
}
