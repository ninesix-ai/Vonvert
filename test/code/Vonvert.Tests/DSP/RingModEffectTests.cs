// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

// ═══════════════════════════════════════════════════════════════════════════
//  RingModEffect — RM-001 ~ RM-007
// ═══════════════════════════════════════════════════════════════════════════

public sealed class RingModEffectTests
{
    [Fact(DisplayName = "RM-001: Default parameters")]
    public void RM001_DefaultParameters()
    {
        var fx = new RingModEffect();
        Assert.Equal("RingMod", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(80f, fx.CarrierFreq);
        Assert.Equal(0.5f, fx.Mix);
        Assert.Equal(0f, fx.HarmonicDepth);
        Assert.Equal(RingModEffect.CarrierWaveform.Sine, fx.Waveform);
    }

    [Fact(DisplayName = "RM-002: Process produces finite output")]
    public void RM002_Process_ProducesFiniteOutput()
    {
        var fx = new RingModEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "RM-003: Enabled → output differs from input")]
    public void RM003_Enabled_OutputDiffers()
    {
        var fx = new RingModEffect { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.01f, $"RingMod should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "RM-004: Reset() makes processing deterministic")]
    public void RM004_Reset_Deterministic()
    {
        var fx = new RingModEffect { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);

        fx.Process(input.AsSpan());
        fx.Reset();

        var buf1 = (float[])input.Clone();
        fx.Process(buf1.AsSpan());

        fx.Reset();
        var buf2 = (float[])input.Clone();
        fx.Process(buf2.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-5f));
    }

    [Fact(DisplayName = "RM-005: Disabled → DSPChain bypass")]
    public void RM005_Disabled_Bypass()
    {
        var fx = new RingModEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "RM-006: Parameter clamping")]
    public void RM006_ParameterClamping()
    {
        var fx = new RingModEffect();

        // CarrierFreq: 10 – 2000
        fx.CarrierFreq = 1f;
        Assert.Equal(10f, fx.CarrierFreq);
        fx.CarrierFreq = 5000f;
        Assert.Equal(2000f, fx.CarrierFreq);

        // Mix: 0 – 1
        fx.Mix = -0.5f;
        Assert.Equal(0f, fx.Mix);
        fx.Mix = 2f;
        Assert.Equal(1f, fx.Mix);

        // HarmonicDepth: 0 – 1
        fx.HarmonicDepth = -1f;
        Assert.Equal(0f, fx.HarmonicDepth);
        fx.HarmonicDepth = 5f;
        Assert.Equal(1f, fx.HarmonicDepth);
    }

    [Fact(DisplayName = "RM-007: All waveforms produce finite output")]
    public void RM007_AllWaveforms_Finite()
    {
        foreach (var waveform in new[] {
            RingModEffect.CarrierWaveform.Sine,
            RingModEffect.CarrierWaveform.Square,
            RingModEffect.CarrierWaveform.Sawtooth,
            RingModEffect.CarrierWaveform.Triangle })
        {
            var fx = new RingModEffect { IsEnabled = true, Waveform = waveform };
            var buf = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);
            fx.Process(buf.AsSpan());
            Assert.All(buf, s => Assert.True(float.IsFinite(s)));
        }
    }
}
