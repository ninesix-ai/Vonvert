// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression for the ITU-R BS.1770-4 loudness meter (pass-through; enabled by default).

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

public sealed class LoudnessMeterEffectTests
{
    [Fact(DisplayName = "LME-001: Default parameters")]
    public void LME001_DefaultParameters()
    {
        var fx = new LoudnessMeterEffect();
        Assert.Equal("LoudnessMeter", fx.Name);
        Assert.True(fx.IsEnabled); // default true
        Assert.Equal(-70f, fx.CurrentMomentary);
        Assert.Equal(-70f, fx.CurrentShortTerm);
        Assert.Equal(-70f, fx.IntegratedLoudness);
    }

    [Fact(DisplayName = "LME-002: Process is pass-through (signal unchanged)")]
    public void LME002_Process_PassThrough()
    {
        var fx = new LoudnessMeterEffect { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(1000, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(original.AsSpan(), input.AsSpan()),
            "LoudnessMeter must not modify the audio signal");
    }

    [Fact(DisplayName = "LME-003: Process produces finite output")]
    public void LME003_Process_ProducesFiniteOutput()
    {
        var fx = new LoudnessMeterEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "LME-004: After full block, momentary loudness updates")]
    public void LME004_AfterFullBlock_MomentaryUpdates()
    {
        var fx = new LoudnessMeterEffect { IsEnabled = true };
        var signal = AudioTestHelpers.GenerateSineWave(1000, 4800, 0.5f);
        fx.Process(signal.AsSpan());

        Assert.True(fx.CurrentMomentary > -70f,
            $"Momentary should update after full block: {fx.CurrentMomentary:G4}");
    }

    [Fact(DisplayName = "LME-005: Reset() restores meters to -70")]
    public void LME005_Reset_RestoresMeters()
    {
        var fx = new LoudnessMeterEffect { IsEnabled = true };
        var signal = AudioTestHelpers.GenerateSineWave(1000, 4800, 0.5f);
        fx.Process(signal.AsSpan());
        Assert.True(fx.CurrentMomentary > -70f);

        fx.Reset();
        Assert.Equal(-70f, fx.CurrentMomentary);
        Assert.Equal(-70f, fx.CurrentShortTerm);
        Assert.Equal(-70f, fx.IntegratedLoudness);
    }

    [Fact(DisplayName = "LME-006: Disabled → DSPChain bypass (still pass-through)")]
    public void LME006_Disabled_Bypass()
    {
        var fx = new LoudnessMeterEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(1000, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }
}
