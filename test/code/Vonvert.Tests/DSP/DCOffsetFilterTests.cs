// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression for the first-chain DC-removal highpass.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

public sealed class DCOffsetFilterTests
{
    [Fact(DisplayName = "DCO-001: Default parameters")]
    public void DCO001_DefaultParameters()
    {
        var fx = new DCOffsetFilter();
        Assert.False(string.IsNullOrEmpty(fx.Name));
    }

    [Fact(DisplayName = "DCO-002: Pure DC signal → offset removed")]
    public void DCO002_DCOffset_Removed()
    {
        var fx = new DCOffsetFilter { IsEnabled = true };
        var buf = new float[4800];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0.3f;   // constant DC offset

        fx.Process(buf.AsSpan());

        // After enough samples the ~5 Hz highpass drives the DC component to ~0.
        double tailRms = AudioTestHelpers.ComputeRms(buf.AsSpan(4000));
        Assert.True(tailRms < 0.05f, $"DC offset should be removed: tail RMS={tailRms:G4}");
    }

    [Fact(DisplayName = "DCO-003: AC signal (zero-mean sine) → minimal alteration")]
    public void DCO003_ACSignal_MinimalAlteration()
    {
        var fx = new DCOffsetFilter { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff < 0.05f,
            $"AC signal should pass through with minimal change: diff={diff:G4}");
    }

    [Fact(DisplayName = "DCO-004: Process preserves buffer length + finite output")]
    public void DCO004_Process_LengthInvariant_Finite()
    {
        var fx = new DCOffsetFilter { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4096, 0.3f, 42);
        int lenBefore = buf.Length;
        fx.Process(buf.AsSpan());
        Assert.Equal(lenBefore, buf.Length);
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }
}
