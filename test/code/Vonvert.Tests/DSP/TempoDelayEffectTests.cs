// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

// ═══════════════════════════════════════════════════════════════════════════
//  TempoDelayEffect — TD-001 ~ TD-007
// ═══════════════════════════════════════════════════════════════════════════

public sealed class TempoDelayEffectTests
{
    [Fact(DisplayName = "TD-001: Default parameters")]
    public void TD001_DefaultParameters()
    {
        var fx = new TempoDelayEffect();
        Assert.Equal("TempoDelay", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(120f, fx.Bpm);
        Assert.Equal(TempoNoteDivision.Eighth, fx.Division);
        Assert.Equal(0.4f, fx.Feedback);
        Assert.Equal(0.3f, fx.Damping);
        Assert.Equal(0.35f, fx.Mix);
    }

    [Fact(DisplayName = "TD-002: Process produces finite output")]
    public void TD002_Process_ProducesFiniteOutput()
    {
        var fx = new TempoDelayEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "TD-003: Enabled → output differs from input")]
    public void TD003_Enabled_OutputDiffers()
    {
        var fx = new TempoDelayEffect { IsEnabled = true, Mix = 0.5f };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.01f, $"TempoDelay should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "TD-004: Reset() makes processing deterministic")]
    public void TD004_Reset_Deterministic()
    {
        var fx = new TempoDelayEffect { IsEnabled = true };
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

    [Fact(DisplayName = "TD-005: Disabled → DSPChain bypass")]
    public void TD005_Disabled_Bypass()
    {
        var fx = new TempoDelayEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "TD-006: GetDelayTimeMs returns correct values")]
    public void TD006_GetDelayTimeMs()
    {
        // At 120 BPM, one beat = 500 ms
        Assert.Equal(2000f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.Whole));
        Assert.Equal(1500f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.HalfDotted));
        Assert.Equal(1000f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.Half));
        Assert.Equal(750f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.QuarterDotted));
        Assert.Equal(500f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.Quarter));
        Assert.Equal(375f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.EighthDotted));
        Assert.Equal(250f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.Eighth));
        Assert.Equal(125f, TempoDelayEffect.GetDelayTimeMs(120f, TempoNoteDivision.Sixteenth));

        // BPM changes: at 60 BPM, one beat = 1000 ms
        Assert.Equal(1000f, TempoDelayEffect.GetDelayTimeMs(60f, TempoNoteDivision.Quarter));
        Assert.Equal(500f, TempoDelayEffect.GetDelayTimeMs(60f, TempoNoteDivision.Eighth));
    }

    [Fact(DisplayName = "TD-007: DelayTimeMs property matches static method")]
    public void TD007_DelayTimeMs_Property()
    {
        var fx = new TempoDelayEffect { Bpm = 140f, Division = TempoNoteDivision.Quarter };
        Assert.Equal(
            TempoDelayEffect.GetDelayTimeMs(140f, TempoNoteDivision.Quarter),
            fx.DelayTimeMs);

        fx.Division = TempoNoteDivision.Sixteenth;
        Assert.Equal(
            TempoDelayEffect.GetDelayTimeMs(140f, TempoNoteDivision.Sixteenth),
            fx.DelayTimeMs);
    }
}
