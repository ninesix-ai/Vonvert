// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

// ═══════════════════════════════════════════════════════════════════════════
//  ModulationDelayEffect — MD-001 ~ MD-006
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ModulationDelayEffectTests
{
    [Fact(DisplayName = "MD-001: Default parameters")]
    public void MD001_DefaultParameters()
    {
        var fx = new ModulationDelayEffect();
        Assert.Equal("ModulationDelay", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(10f, fx.BaseDelayMs);
        Assert.Equal(0.5f, fx.ModDepth);
        Assert.Equal(0.3f, fx.Feedback);
        Assert.Equal(0.4f, fx.Mix);
    }

    [Fact(DisplayName = "MD-002: Process produces finite output")]
    public void MD002_Process_ProducesFiniteOutput()
    {
        var fx = new ModulationDelayEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "MD-003: Enabled → output differs from input")]
    public void MD003_Enabled_OutputDiffers()
    {
        var fx = new ModulationDelayEffect { IsEnabled = true, Mix = 0.5f };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.01f, $"ModulationDelay should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "MD-004: Reset() makes processing deterministic")]
    public void MD004_Reset_Deterministic()
    {
        var fx = new ModulationDelayEffect { IsEnabled = true };
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

    [Fact(DisplayName = "MD-005: Disabled → DSPChain bypass")]
    public void MD005_Disabled_Bypass()
    {
        var fx = new ModulationDelayEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "MD-006: Parameter clamping")]
    public void MD006_ParameterClamping()
    {
        var fx = new ModulationDelayEffect();

        // BaseDelayMs: 1 – 50
        fx.BaseDelayMs = 0f;
        Assert.Equal(1f, fx.BaseDelayMs);
        fx.BaseDelayMs = 100f;
        Assert.Equal(50f, fx.BaseDelayMs);

        // ModDepth: 0 – 1
        fx.ModDepth = -1f;
        Assert.Equal(0f, fx.ModDepth);
        fx.ModDepth = 2f;
        Assert.Equal(1f, fx.ModDepth);

        // Feedback: 0 – 0.85
        fx.Feedback = -1f;
        Assert.Equal(0f, fx.Feedback);
        fx.Feedback = 1f;
        Assert.Equal(0.85f, fx.Feedback);

        // Mix: 0 – 1
        fx.Mix = -1f;
        Assert.Equal(0f, fx.Mix);
        fx.Mix = 2f;
        Assert.Equal(1f, fx.Mix);
    }

    [Fact(DisplayName = "MD-007: ModDepth actually modulates the delay time")]
    public void MD007_Depth_AffectsDelay()
    {
        // The whole point of this effect is that the source envelope moves the
        // delay time; a flat-delay implementation must not pass.
        static float[] Run(float depth)
        {
            var fx = new ModulationDelayEffect
            {
                IsEnabled = true, BaseDelayMs = 10f, ModDepth = depth,
                Feedback = 0f, Mix = 1f,
            };
            var buf = AudioTestHelpers.GenerateVoiceLike(120f, 4800, 0.4f);   // syllable envelope
            fx.Process(buf.AsSpan());
            return buf;
        }

        Assert.False(AudioTestHelpers.SpanEqual(Run(0f), Run(1f)),
            "ModDepth=1 must bend the delay time relative to ModDepth=0");
    }

    [Fact(DisplayName = "MD-008: Feedback sustains the wet tail")]
    public void MD008_Feedback_SustainsTail()
    {
        static float TailRms(float feedback)
        {
            var fx = new ModulationDelayEffect
            {
                IsEnabled = true, BaseDelayMs = 10f, ModDepth = 0f,
                Feedback = feedback, Mix = 1f,
            };
            var buf = AudioTestHelpers.GenerateImpulse(9600, 0, 1f);   // 200 ms
            fx.Process(buf.AsSpan());
            return AudioTestHelpers.ComputeRMS(buf.AsSpan(4800));      // last 100 ms
        }

        Assert.True(TailRms(0.8f) > TailRms(0f) * 2f,
            $"feedback=0.8 should ring far longer than 0, got {TailRms(0.8f):G4} vs {TailRms(0f):G4}");
    }
}
