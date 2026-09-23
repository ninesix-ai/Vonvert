// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// The pitch shifter is the core of the voice-changing feature: +12 semitones
// must raise a 220 Hz tone to 440 Hz, -12 semitones must lower 880 Hz to 440 Hz.

namespace Vonvert.Tests.DSP;

using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Tests.Helpers;
using Xunit;

public class PitchShiftEffectTests
{
    [Fact]
    public void PS_001_DefaultSemitonesZero_IsBypass()
    {
        var fx = new VxPitch();
        Assert.Equal(0f, fx.Semitones);
        var input = AudioTestHelpers.GenerateSine(220f, 1024);
        var expected = new float[input.Length];
        Array.Copy(input, expected, input.Length);
        fx.IsEnabled = true;
        fx.Process(input);
        AssertExtensions.SpanEqual<float>(expected, input);
    }

    [Fact]
    public void PS_002_Up12Semitones_220To440()
    {
        var fx = new VxPitch { IsEnabled = true, Semitones = 12f };
        const int totalSamples = 16384;
        var input = AudioTestHelpers.GenerateSine(220f, totalSamples);
        fx.Process(input);
        int half = totalSamples / 2;
        var tail = input.AsSpan(half);
        double freq = AudioTestHelpers.EstimateDominantFrequency(tail);
        Assert.InRange(freq, 440 - 20, 440 + 20);
    }

    [Fact]
    public void PS_003_Down12Semitones_880To440()
    {
        var fx = new VxPitch { IsEnabled = true, Semitones = -12f };
        const int totalSamples = 16384;
        var input = AudioTestHelpers.GenerateSine(880f, totalSamples);
        fx.Process(input);
        int half = totalSamples / 2;
        var tail = input.AsSpan(half);
        double freq = AudioTestHelpers.EstimateDominantFrequency(tail);
        Assert.InRange(freq, 440 - 20, 440 + 20);
    }

    [Fact]
    public void PS_004_Clamp_Upper24()
    {
        var fx = new VxPitch();
        fx.Semitones = 100f;
        Assert.Equal(24f, fx.Semitones);
    }

    [Fact]
    public void PS_005_Clamp_LowerMinus24()
    {
        var fx = new VxPitch();
        fx.Semitones = -100f;
        Assert.Equal(-24f, fx.Semitones);
    }

    [Fact]
    public void PS_007_Reset_OutputConsistent_RmsDiffWithin10Percent()
    {
        static double RunOnce(float semitones)
        {
            var fx = new VxPitch { IsEnabled = true, Semitones = semitones };
            const int samples = 16384;
            var sig = AudioTestHelpers.GenerateSine(440f, samples);
            for (int i = 0; i < 8; i++)
                fx.Process(sig.AsSpan(i * 2048, 2048));
            var out1 = new float[samples];
            Array.Copy(sig, out1, samples);
            fx.Reset();
            sig = AudioTestHelpers.GenerateSine(440f, samples);
            for (int i = 0; i < 8; i++)
                fx.Process(sig.AsSpan(i * 2048, 2048));
            var out2 = new float[samples];
            Array.Copy(sig, out2, samples);
            double rms1 = AudioTestHelpers.ComputeRms(out1.AsSpan(8192));
            double rms2 = AudioTestHelpers.ComputeRms(out2.AsSpan(8192));
            double max = Math.Max(rms1, rms2);
            if (max < 1e-9) return 0;
            return Math.Abs(rms1 - rms2) / max;
        }
        double diff = RunOnce(7f);
        Assert.True(diff < 0.10, $"RMS diff after reset = {diff:P2}, expected < 10%");
    }

    [Fact]
    public void PS_008_SilentInput_SilentOutput()
    {
        var fx = new VxPitch { IsEnabled = true, Semitones = 7f };
        for (int i = 0; i < 32; i++)
            fx.Process(new float[1024]);
        var silence = new float[1024];
        fx.Process(silence);
        AssertExtensions.AllSilent(silence);
    }

    [Fact]
    public void PS_009_Disabled_FullBypass_SpanEqual()
    {
        // IAudioEffect contract: IsEnabled is checked by DSPChain, not by the effect
        // itself inside Process(). So we verify through DSPChain that when
        // IsEnabled=false the buffer passes through completely unchanged.
        var fx = new VxPitch { IsEnabled = false, Semitones = 7f };
        var chain = new DSPChain();
        chain.Add(fx);

        var input = AudioTestHelpers.GenerateSine(440f, 1024);
        var expected = new float[input.Length];
        Array.Copy(input, expected, input.Length);

        chain.Process(input);
        AssertExtensions.SpanEqual<float>(expected, input);
    }

    private sealed class FakeAutoSource : Vonvert.Engine.AudioEngine.IAutoPitchSource
    {
        public bool Enabled => true;
        public bool HasTarget => true;
        public float Semitones { get; set; }
    }

    [Fact]
    public void PS_010_AutoSource_DrivesEffectiveSemitones()
    {
        var fx = new VxPitch { IsEnabled = true, Semitones = 0f, AutoTargetEnabled = true,
                               AutoSource = new FakeAutoSource { Semitones = 7f } };
        var sig = AudioTestHelpers.GenerateSine(220f, 16384);
        fx.Process(sig);
        double hz = AudioTestHelpers.EstimateDominantFrequency(sig.AsSpan(8192));
        // +7 → ×2^(7/12)≈1.498 → ~330 Hz
        Assert.InRange(hz, 300, 360);
    }

    [Fact]
    public void PS_011_AutoDisabled_UsesManualSemitones()
    {
        var fx = new VxPitch { IsEnabled = true, Semitones = 4f, AutoTargetEnabled = false,
                               AutoSource = new FakeAutoSource { Semitones = 12f } };
        var sig = AudioTestHelpers.GenerateSine(180f, 16384);
        fx.Process(sig);
        double hz = AudioTestHelpers.EstimateDominantFrequency(sig.AsSpan(8192));
        Assert.InRange(hz, 200, 260); // +4 → ~227, source ignored
    }
}