// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm + parameter-clamping regression for the band-split de-esser.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

public sealed class DeesserEffectTests
{
    [Fact(DisplayName = "DE-001: Default parameters")]
    public void DE001_DefaultParameters()
    {
        var fx = new DeesserEffect();
        Assert.Equal("VoiceDeesser", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(6000f, fx.Frequency);
        Assert.Equal(-18f, fx.ThresholdDb);
        Assert.Equal(4f, fx.Ratio);
        Assert.Equal(2.0f, fx.BandwidthQ);
    }

    [Fact(DisplayName = "DE-002: Process produces finite output")]
    public void DE002_Process_ProducesFiniteOutput()
    {
        var fx = new DeesserEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.5f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "DE-003: Enabled → output differs from input")]
    public void DE003_Enabled_OutputDiffers()
    {
        var fx = new DeesserEffect
        {
            IsEnabled = true,
            Frequency = 6000f,
            ThresholdDb = -18f,
            Ratio = 4f,
        };

        var input = AudioTestHelpers.GenerateSineWave(6000, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.01f, $"De-esser should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "DE-004: Reset() makes processing deterministic")]
    public void DE004_Reset_Deterministic()
    {
        var fx = new DeesserEffect { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(6000, 2400, 0.5f);

        fx.Process(input.AsSpan());
        fx.Reset();

        var buf1 = (float[])input.Clone();
        fx.Process(buf1.AsSpan());

        fx.Reset();
        var buf2 = (float[])input.Clone();
        fx.Process(buf2.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-5f));
    }

    [Fact(DisplayName = "DE-005: Disabled → DSPChain bypass")]
    public void DE005_Disabled_Bypass()
    {
        var fx = new DeesserEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(6000, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "DE-006: Parameter clamping")]
    public void DE006_ParameterClamping()
    {
        var fx = new DeesserEffect();

        fx.Frequency = 500f;   Assert.Equal(2000f, fx.Frequency);
        fx.Frequency = 20000f; Assert.Equal(12000f, fx.Frequency);

        fx.ThresholdDb = -60f; Assert.Equal(-40f, fx.ThresholdDb);
        fx.ThresholdDb = 10f;  Assert.Equal(0f, fx.ThresholdDb);

        fx.Ratio = 0f;  Assert.Equal(1f, fx.Ratio);
        fx.Ratio = 50f; Assert.Equal(20f, fx.Ratio);

        fx.BandwidthQ = 0.1f; Assert.Equal(0.5f, fx.BandwidthQ);
        fx.BandwidthQ = 20f;  Assert.Equal(8.0f, fx.BandwidthQ);
    }
}
