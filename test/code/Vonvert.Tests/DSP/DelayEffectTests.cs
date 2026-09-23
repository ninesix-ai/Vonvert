// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm + parameter-clamping regression for the feedback/ping-pong delay.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Tests.Helpers;

public sealed class DelayEffectTests
{
    [Fact(DisplayName = "DL-001: Default parameters")]
    public void DL001_DefaultParameters()
    {
        var fx = new DelayEffect();
        Assert.Equal("VoiceDelay", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(300f, fx.TimeMs);
        Assert.Equal(0.35f, fx.Feedback);
        Assert.Equal(0.3f, fx.Mix);
        Assert.Equal(0.5f, fx.Damping);
        Assert.False(fx.PingPong);
    }

    [Fact(DisplayName = "DL-002: Process produces finite output")]
    public void DL002_Process_ProducesFiniteOutput()
    {
        var fx = new DelayEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "DL-003: Enabled → output differs from input")]
    public void DL003_Enabled_OutputDiffers()
    {
        var fx = new DelayEffect { IsEnabled = true, Mix = 0.5f, Feedback = 0.3f };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.01f, $"Delay should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "DL-004: Reset() makes processing deterministic")]
    public void DL004_Reset_Deterministic()
    {
        var fx = new DelayEffect { IsEnabled = true };
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

    [Fact(DisplayName = "DL-005: Disabled → DSPChain bypass")]
    public void DL005_Disabled_Bypass()
    {
        var fx = new DelayEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "DL-006: Parameter clamping")]
    public void DL006_ParameterClamping()
    {
        var fx = new DelayEffect();

        fx.TimeMs = 1f;    Assert.Equal(10f, fx.TimeMs);
        fx.TimeMs = 5000f; Assert.Equal(1500f, fx.TimeMs);

        fx.Feedback = -1f; Assert.Equal(0f, fx.Feedback);
        fx.Feedback = 1f;  Assert.Equal(0.95f, fx.Feedback);

        fx.Mix = -1f; Assert.Equal(0f, fx.Mix);
        fx.Mix = 2f;  Assert.Equal(1f, fx.Mix);

        fx.Damping = -1f; Assert.Equal(0f, fx.Damping);
        fx.Damping = 2f;  Assert.Equal(1f, fx.Damping);
    }

    [Fact(DisplayName = "DL-007: PingPong mode produces finite output")]
    public void DL007_PingPong_FiniteOutput()
    {
        var fx = new DelayEffect { IsEnabled = true, PingPong = true, Mix = 0.4f };
        var buf = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }
}
