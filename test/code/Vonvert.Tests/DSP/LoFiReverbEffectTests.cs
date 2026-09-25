// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

// ═══════════════════════════════════════════════════════════════════════════
//  LoFiReverbEffect — LR-001 ~ LR-010
// ═══════════════════════════════════════════════════════════════════════════

public sealed class LoFiReverbEffectTests
{
    [Fact(DisplayName = "LR-001: Default parameters")]
    public void LR001_DefaultParameters()
    {
        var fx = new LoFiReverbEffect();
        Assert.Equal("LoFiReverb", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(0.5f, fx.RoomSize);
        Assert.Equal(0.6f, fx.Decay);
        Assert.Equal(4, fx.Downsample);
        Assert.Equal(8f, fx.BitCrush);
        Assert.Equal(0.35f, fx.Mix);
    }

    [Fact(DisplayName = "LR-002: Process produces finite output")]
    public void LR002_Process_ProducesFiniteOutput()
    {
        var fx = new LoFiReverbEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "LR-003: Enabled → output differs from input")]
    public void LR003_Enabled_OutputDiffers()
    {
        var fx = new LoFiReverbEffect { IsEnabled = true, Mix = 0.5f };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff > 0.001f, $"LoFiReverb should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "LR-004: Reset() makes processing deterministic")]
    public void LR004_Reset_Deterministic()
    {
        var fx = new LoFiReverbEffect { IsEnabled = true };
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

    [Fact(DisplayName = "LR-005: Disabled → DSPChain bypass")]
    public void LR005_Disabled_Bypass()
    {
        var fx = new LoFiReverbEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()));
    }

    [Fact(DisplayName = "LR-006: Parameter clamping")]
    public void LR006_ParameterClamping()
    {
        var fx = new LoFiReverbEffect();

        // RoomSize: 0 – 1
        fx.RoomSize = -1f;
        Assert.Equal(0f, fx.RoomSize);
        fx.RoomSize = 2f;
        Assert.Equal(1f, fx.RoomSize);

        // Decay: 0 – 0.95
        fx.Decay = -1f;
        Assert.Equal(0f, fx.Decay);
        fx.Decay = 1f;
        Assert.Equal(0.95f, fx.Decay);

        // Downsample: 1 – 20
        fx.Downsample = 0;
        Assert.Equal(1, fx.Downsample);
        fx.Downsample = 50;
        Assert.Equal(20, fx.Downsample);

        // BitCrush: 1 – 16
        fx.BitCrush = 0f;
        Assert.Equal(1f, fx.BitCrush);
        fx.BitCrush = 32f;
        Assert.Equal(16f, fx.BitCrush);

        // Mix: 0 – 1
        fx.Mix = -1f;
        Assert.Equal(0f, fx.Mix);
        fx.Mix = 2f;
        Assert.Equal(1f, fx.Mix);
    }

    [Fact(DisplayName = "LR-007: Mix=0 → pass-through")]
    public void LR007_MixZero_PassThrough()
    {
        var fx = new LoFiReverbEffect { IsEnabled = true, Mix = 0f };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        // With mix=0, dry=1, wet=0 → output = input * 1 + degraded * 0 = input
        Assert.True(AudioTestHelpers.SpanEqual(original.AsSpan(), input.AsSpan()),
            "Mix=0 should produce pass-through");
    }

    [Fact(DisplayName = "LR-008: larger Decay leaves more energy 100 ms after an impulse")]
    public void LR008_Decay_ControlsTailLength()
    {
        static float TailRms(float decay)
        {
            var fx = new LoFiReverbEffect
            {
                IsEnabled = true, Decay = decay, RoomSize = 0.5f,
                Mix = 1f, Downsample = 1, BitCrush = 16f,
            };
            var buf = AudioTestHelpers.GenerateImpulse(9600, 0, 1f);   // 200 ms
            fx.Process(buf.AsSpan());
            return AudioTestHelpers.ComputeRMS(buf.AsSpan(4800));      // last 100 ms
        }

        float longTail = TailRms(0.9f);
        float shortTail = TailRms(0.3f);
        Assert.True(longTail > shortTail * 2f,
            $"Decay must set tail length: decay=0.9 -> {longTail:G4}, decay=0.3 -> {shortTail:G4}");
    }

    [Fact(DisplayName = "LR-009: extreme settings stay finite and bounded (no runaway feedback)")]
    public void LR009_ExtremeSettings_Bounded()
    {
        var fx = new LoFiReverbEffect
        {
            IsEnabled = true, Decay = 0.95f, RoomSize = 1f, Mix = 1f,
            Downsample = 1, BitCrush = 1f,
        };
        var buf = AudioTestHelpers.GenerateWhiteNoise(48000, 0.5f, 7);   // 1 s
        fx.Process(buf.AsSpan());

        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
        float peak = AudioTestHelpers.ComputePeak(buf.AsSpan());
        Assert.True(peak < 4f, $"wet path must stay bounded at decay=0.95, peak={peak:G4}");
    }

    [Fact(DisplayName = "LR-010: RoomSize changes the comb delay lengths (output differs)")]
    public void LR010_RoomSize_ChangesTail()
    {
        static float[] Run(float roomSize)
        {
            var fx = new LoFiReverbEffect
            {
                IsEnabled = true, RoomSize = roomSize, Decay = 0.7f,
                Mix = 1f, Downsample = 1, BitCrush = 16f,
            };
            var buf = AudioTestHelpers.GenerateImpulse(4800, 0, 1f);   // 100 ms
            fx.Process(buf.AsSpan());
            return buf;
        }

        Assert.False(AudioTestHelpers.SpanEqual(Run(0f), Run(1f)),
            "RoomSize must alter the reverb structure");
    }

    [Fact(DisplayName = "LR-011: tail level stays constant as Decay lengthens it")]
    public void LR011_Decay_KeepsDiffuseLevelConstant()
    {
        static float SteadyRms(float decay)
        {
            var fx = new LoFiReverbEffect
            {
                IsEnabled = true, Decay = decay, RoomSize = 0.5f,
                Mix = 1f, Downsample = 1, BitCrush = 16f,
            };
            var buf = AudioTestHelpers.GenerateWhiteNoise(48000, 0.5f, 11);   // 1 s
            fx.Process(buf.AsSpan());
            return AudioTestHelpers.ComputeRMS(buf.AsSpan(24000));           // settled half
        }

        float quietDecay = SteadyRms(0.2f);
        float longDecay  = SteadyRms(0.9f);
        // Within +/-2 dB: the diffuse noise gain must not ride the comb's
        // 1/sqrt(1-feedback^2) growth, or every persona's Decay change would also
        // be a loudness change.
        float ratio = longDecay / quietDecay;
        Assert.True(ratio is > 0.79f and < 1.26f,
            $"decay=0.9 vs 0.2 level ratio must stay near unity, got {ratio:G4} (quiet={quietDecay:G4}, long={longDecay:G4})");
    }

    [Fact(DisplayName = "LR-012: RoomSize sets the first comb arrival index exactly")]
    public void LR012_RoomSize_SetsTapLength()
    {
        static int FirstNonZeroSample(float roomSize)
        {
            // Base taps are 600/720/840/960 scaled by 1 + 2*roomSize; the shortest
            // comb decides the first wet sample. Reset() snaps the taps to their
            // target so the slew cannot smear the arrival index.
            var fx = new LoFiReverbEffect
            {
                IsEnabled = true, RoomSize = roomSize, Decay = 0.7f,
                Mix = 1f, Downsample = 1, BitCrush = 16f,
            };
            fx.Reset();
            var buf = AudioTestHelpers.GenerateImpulse(4800, 0, 1f);
            fx.Process(buf.AsSpan());
            for (int i = 0; i < buf.Length; i++)
                if (buf[i] != 0f) return i;
            return -1;
        }

        Assert.Equal(600,  FirstNonZeroSample(0f));    // 600 * 1.0
        Assert.Equal(1800, FirstNonZeroSample(1f));    // 600 * 3.0
    }
}
