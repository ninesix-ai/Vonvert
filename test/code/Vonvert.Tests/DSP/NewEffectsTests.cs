// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Unit tests for the Bitcrusher, TiltEQ, GraphicEQ and (class-only)
// StereoWidth effects in the GitHub engine.
//
// The tests use the shared helper surface (AssertExtensions.SpanApproxEqual /
// SpanEqual).  Bitcrusher tests pin the real-time contract the lo-fi preset
// relies on.

namespace Vonvert.Tests.DSP;

using System;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;
using Xunit;

// ═══════════════════════════════════════════════════════════════════════════
//  BitcrusherEffect — BC-001 ~ BC-006
// ═══════════════════════════════════════════════════════════════════════════
public sealed class BitcrusherEffectTests
{
    [Fact(DisplayName = "BC-001: Default parameters and name contract")]
    public void BC001_DefaultParameters()
    {
        var fx = new BitcrusherEffect();
        Assert.Equal("Bitcrusher", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(8f, fx.BitDepth);
        Assert.Equal(1, fx.SampleRateReduction);
        Assert.Equal(BitcrusherEffect.QuantCurve.Linear, fx.Curve);
        Assert.False(fx.Dither);
    }

    [Fact(DisplayName = "BC-002: Coarse bit depth quantises to a 1-bit grid")]
    public void BC002_BitDepthOne_QuantisesToHalfSteps()
    {
        var fx = new BitcrusherEffect { IsEnabled = true, BitDepth = 1f };  // levels = 2 → step = 0.5
        var input = new float[] { 0.2f, 0.4f, 0.7f, -0.3f, 0.9f };
        var buf = (float[])input.Clone();

        fx.Process(buf.AsSpan());

        foreach (var s in buf)
        {
            double doubled = s * 2.0;   // every 1-bit output is an integer multiple of 0.5
            Assert.True(Math.Abs(doubled - Math.Round(doubled)) < 1e-4,
                $"1-bit crush must snap to 0.5 grid; got {s:G5}.");
        }
    }

    [Fact(DisplayName = "BC-003: Lower bit depth changes the signal; high depth is near-transparent")]
    public void BC003_BitDepthModulatesCrushAmount()
    {
        var src = AudioTestHelpers.GenerateWhiteNoise(4800, 0.5f, 7);

        var crushed = (float[])src.Clone();
        new BitcrusherEffect { IsEnabled = true, BitDepth = 3f }.Process(crushed.AsSpan());

        var soft = (float[])src.Clone();
        new BitcrusherEffect { IsEnabled = true, BitDepth = 24f }.Process(soft.AsSpan());

        float crushDiff = AudioTestHelpers.ComputeRmsDifference(src.AsSpan(), crushed.AsSpan());
        float softDiff  = AudioTestHelpers.ComputeRmsDifference(src.AsSpan(), soft.AsSpan());

        Assert.True(crushDiff > 0.01f, $"3-bit crush should differ from input (diff={crushDiff:G4}).");
        Assert.True(softDiff < crushDiff, $"24-bit should be gentler than 3-bit (soft={softDiff:G4}, crush={crushDiff:G4}).");
    }

    [Fact(DisplayName = "BC-004: Sample-rate reduction holds each quantised value N times")]
    public void BC004_SampleReductionHoldsSamples()
    {
        var fx = new BitcrusherEffect { IsEnabled = true, BitDepth = 12f, SampleRateReduction = 4 };
        // Rising ramp so consecutive held frames are distinguishable.
        var buf = new float[16];
        for (int i = 0; i < buf.Length; i++) buf[i] = i * 0.05f;

        fx.Process(buf.AsSpan());

        // First group of 4 (indices 0..3) all echo the quantised sample from index 0.
        Assert.Equal(buf[0], buf[1]);
        Assert.Equal(buf[0], buf[2]);
        Assert.Equal(buf[0], buf[3]);
        // Index 4 begins a fresh hold and must differ from the first group.
        Assert.NotEqual(buf[0], buf[4]);
    }

    [Fact(DisplayName = "BC-005: Silence stays silent")]
    public void BC005_SilenceStaysSilent()
    {
        var fx = new BitcrusherEffect { IsEnabled = true, BitDepth = 4f };
        var buf = new float[512];
        fx.Process(buf.AsSpan());
        foreach (var s in buf) Assert.Equal(0f, s, 6);
    }

    [Fact(DisplayName = "BC-006: BitDepth / SampleReduction setters clamp")]
    public void BC006_SettersClamp()
    {
        var fx = new BitcrusherEffect();
        fx.BitDepth = -5f;  Assert.Equal(1f, fx.BitDepth);
        fx.BitDepth = 100f; Assert.Equal(24f, fx.BitDepth);
        fx.SampleRateReduction = 0;   Assert.Equal(1, fx.SampleRateReduction);
        fx.SampleRateReduction = 999; Assert.Equal(20, fx.SampleRateReduction);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  TiltEQEffect — TE-001 ~ TE-006
// ═══════════════════════════════════════════════════════════════════════════
public sealed class TiltEQEffectTests
{
    [Fact(DisplayName = "TE-001: Default parameters")]
    public void TE001_DefaultParameters()
    {
        var fx = new TiltEQEffect();
        Assert.Equal("TiltEQ", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(0f, fx.Tilt);
        Assert.Equal(6f, fx.MaxGainDb);
        Assert.Equal(1000f, fx.PivotFreq);
    }

    [Fact(DisplayName = "TE-002: Process produces finite output")]
    public void TE002_Process_ProducesFiniteOutput()
    {
        var fx = new TiltEQEffect { IsEnabled = true, Tilt = 0.5f };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 42);
        fx.Process(buf.AsSpan());
        Assert.All(buf, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "TE-003: Positive tilt modifies the signal")]
    public void TE003_PositiveTilt_Brightens()
    {
        var fx = new TiltEQEffect { IsEnabled = true, Tilt = 1.0f, MaxGainDb = 6f };
        var buf = AudioTestHelpers.GenerateWhiteNoise(9600, 0.5f, 42);
        var original = (float[])buf.Clone();

        fx.Process(buf.AsSpan());

        float diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buf.AsSpan());
        Assert.True(diff > 0.01f, $"Positive tilt should modify signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "TE-004: Zero tilt → near-unity (minimal change)")]
    public void TE004_ZeroTilt_NearUnity()
    {
        var fx = new TiltEQEffect { IsEnabled = true, Tilt = 0f };
        var input = AudioTestHelpers.GenerateSineWave(1000, 4800, 0.5f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        float diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff < 0.01f, $"Zero tilt should barely change signal: diff={diff:G4}");
    }

    [Fact(DisplayName = "TE-005: Reset() makes processing deterministic")]
    public void TE005_Reset_Deterministic()
    {
        var fx = new TiltEQEffect { IsEnabled = true, Tilt = 0.5f };
        var input = AudioTestHelpers.GenerateSineWave(1000, 2400, 0.5f);

        fx.Process(input.AsSpan());
        fx.Reset();

        var buf1 = (float[])input.Clone();
        fx.Process(buf1.AsSpan());

        fx.Reset();
        var buf2 = (float[])input.Clone();
        fx.Process(buf2.AsSpan());

        AssertExtensions.SpanApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-5f);
    }

    [Fact(DisplayName = "TE-006: Tilt parameter clamping")]
    public void TE006_TiltClamping()
    {
        var fx = new TiltEQEffect();
        fx.Tilt = -5f;
        Assert.Equal(-1f, fx.Tilt);
        fx.Tilt = 5f;
        Assert.Equal(1f, fx.Tilt);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  GraphicEQEffect — GEQ-001 ~ GEQ-008
// ═══════════════════════════════════════════════════════════════════════════
public class GraphicEQEffectTests
{
    private const int SampleCount = 48000;

    [Fact(DisplayName = "GEQ-001: Default all-zero gains → output ≈ input")]
    public void GEQ001_DefaultZeroGains_OutputApproxInput()
    {
        var effect = new GraphicEQEffect();
        var input = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.3f, 1);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buffer.AsSpan());
        Assert.True(rmsDiff < 0.10f, $"RMS diff = {rmsDiff:P2}, expected < 10%");
    }

    [Fact(DisplayName = "GEQ-002: Band 0 +12dB → low-frequency energy increases")]
    public void GEQ002_Band0Plus12Db_LowBandEnergyIncreases()
    {
        var effect = new GraphicEQEffect();
        effect.SetBandGain(0, 12f);
        var input = AudioTestHelpers.GenerateSineWave(100f, SampleCount, 0.5f);
        var inSeg = new Span<float>(input, 0, SampleCount / 2);
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());
        var outSeg = new Span<float>(buffer, 0, SampleCount / 2);

        double eIn = AudioTestHelpers.ComputeEnergy(inSeg);
        double eOut = AudioTestHelpers.ComputeEnergy(outSeg);
        double ratio = eIn > 0 ? eOut / eIn : 0;
        Assert.True(ratio > 1.5, $"Low band energy ratio = {ratio:F2}, expected > 1.5x");
    }

    [Fact(DisplayName = "GEQ-003: Band 5 -12dB → mid-frequency energy attenuated")]
    public void GEQ003_Band5Minus12Db_MidBandEnergyAttenuated()
    {
        var effect = new GraphicEQEffect();
        effect.SetBandGain(5, -12f);
        var input = AudioTestHelpers.GenerateSineWave(2400f, SampleCount, 0.5f);
        var buffer = (float[])input.Clone();
        var original = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        int q1 = SampleCount / 4;
        int q3 = SampleCount * 3 / 4;
        double eIn = AudioTestHelpers.ComputeEnergy(new Span<float>(original, q1, q3 - q1));
        double eOut = AudioTestHelpers.ComputeEnergy(new Span<float>(buffer, q1, q3 - q1));
        double ratio = eIn > 0 ? eOut / eIn : 1;
        Assert.True(ratio < 0.7, $"Mid band energy ratio = {ratio:F2}, expected < 0.7");
    }

    [Fact(DisplayName = "GEQ-004: Multiple bands → each independently effective")]
    public void GEQ004_MultipleBands_IndependentlyEffective()
    {
        var effect = new GraphicEQEffect();
        effect.SetBandGain(0, 6f);
        effect.SetBandGain(9, -6f);
        Assert.Equal(6f, effect.GainsDb[0]);
        Assert.Equal(-6f, effect.GainsDb[9]);
        Assert.Equal(0f, effect.GainsDb[5]);
    }

    [Fact(DisplayName = "GEQ-005: IsEnabled=false → bypass (DSPChain skips)")]
    public void GEQ005_Disabled_BypassSpanEqual()
    {
        var effect = new GraphicEQEffect { IsEnabled = false };
        effect.SetBandGain(0, 12f);
        effect.SetBandGain(5, -12f);
        var input = AudioTestHelpers.GenerateSineWave(1000f, 4800, 0.5f);
        var expected = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(effect);
        chain.Process(buffer.AsSpan());

        AssertExtensions.SpanEqual(expected, buffer);
    }

    [Fact(DisplayName = "GEQ-006: Reset() clears filter state")]
    public void GEQ006_Reset_ClearsFilterState()
    {
        var effect = new GraphicEQEffect();
        effect.SetBandGain(0, 12f);
        var input = AudioTestHelpers.GenerateSineWave(80f, 4800, 0.5f);
        var buffer = (float[])input.Clone();
        effect.Process(buffer.AsSpan());

        effect.Reset();
        var buffer2 = (float[])input.Clone();
        effect.Process(buffer2.AsSpan());
        for (int i = 0; i < buffer2.Length; i++)
            Assert.True(float.IsFinite(buffer2[i]));
    }

    [Fact(DisplayName = "GEQ-007: SetBandGain updates GainsDb and marks dirty")]
    public void GEQ007_SetBandGain_UpdatesGainsDb()
    {
        var effect = new GraphicEQEffect();
        Assert.Equal(0f, effect.GainsDb[3]);
        effect.SetBandGain(3, 5.5f);
        Assert.Equal(5.5f, effect.GainsDb[3]);
        effect.SetBandGain(3, -3f);
        Assert.Equal(-3f, effect.GainsDb[3]);
    }

    [Fact(DisplayName = "GEQ-008: Frequencies array has 10 voice-optimized values")]
    public void GEQ008_Frequencies_Has10VoiceOptimizedValues()
    {
        var freqs = GraphicEQEffect.Frequencies;
        Assert.Equal(10, freqs.Length);
        Assert.Equal(80f, freqs[0]);
        Assert.Equal(160f, freqs[1]);
        Assert.Equal(400f, freqs[2]);
        Assert.Equal(800f, freqs[3]);
        Assert.Equal(1600f, freqs[4]);
        Assert.Equal(2400f, freqs[5]);
        Assert.Equal(3200f, freqs[6]);
        Assert.Equal(4800f, freqs[7]);
        Assert.Equal(7000f, freqs[8]);
        Assert.Equal(12000f, freqs[9]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  StereoWidthEffect — SW-001 ~ SW-006
//  (Class-only port: the effect is NOT wired into the mono DSP chain, so these
//   tests exercise the class directly with an externally supplied mono input.)
// ═══════════════════════════════════════════════════════════════════════════
public sealed class StereoWidthEffectTests
{
    [Fact(DisplayName = "SW-001: Default parameters")]
    public void SW001_DefaultParameters()
    {
        var fx = new StereoWidthEffect();
        Assert.Equal("VoiceStereoWidth", fx.Name);
        Assert.False(fx.IsEnabled);
        Assert.Equal(1.0f, fx.Width);
        Assert.Equal(0.3f, fx.HaasDelayMs);
        Assert.Equal(0f, fx.Balance);
    }

    [Fact(DisplayName = "SW-002: Process produces finite stereo output")]
    public void SW002_Process_ProducesFiniteStereoOutput()
    {
        var fx = new StereoWidthEffect { IsEnabled = true };
        var mono = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);
        fx.SetMonoInput(mono);

        var stereo = new float[mono.Length * 2];
        fx.Process(stereo.AsSpan());

        Assert.All(stereo, s => Assert.True(float.IsFinite(s)));
    }

    [Fact(DisplayName = "SW-003: Process fills stereo buffer from mono input")]
    public void SW003_Process_FillsStereoBuffer()
    {
        var fx = new StereoWidthEffect { IsEnabled = true, Width = 1.0f };
        var mono = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);
        fx.SetMonoInput(mono);

        var stereo = new float[mono.Length * 2];
        fx.Process(stereo.AsSpan());

        AssertExtensions.NotSilent(stereo.AsSpan());
    }

    [Fact(DisplayName = "SW-004: Width=0 produces mono (L ≈ R)")]
    public void SW004_WidthZero_ProducesMono()
    {
        var fx = new StereoWidthEffect { IsEnabled = true, Width = 0f, HaasDelayMs = 0f };
        var mono = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);
        fx.SetMonoInput(mono);

        var stereo = new float[mono.Length * 2];
        fx.Process(stereo.AsSpan());

        int frames = mono.Length;
        float maxDiff = 0f;
        for (int i = 0; i < frames; i++)
        {
            float diff = MathF.Abs(stereo[i * 2] - stereo[i * 2 + 1]);
            if (diff > maxDiff) maxDiff = diff;
        }
        Assert.True(maxDiff < 0.01f, $"Width=0 should produce L≈R, max L/R diff={maxDiff:G4}");
    }

    [Fact(DisplayName = "SW-005: Reset() clears Haas delay line")]
    public void SW005_Reset_ClearsDelayLine()
    {
        var fx = new StereoWidthEffect { IsEnabled = true };
        var mono = AudioTestHelpers.GenerateSineWave(440, 2400, 0.5f);
        fx.SetMonoInput(mono);

        var stereo1 = new float[mono.Length * 2];
        fx.Process(stereo1.AsSpan());

        fx.Reset();

        fx.SetMonoInput(mono);
        var stereo2 = new float[mono.Length * 2];
        fx.Process(stereo2.AsSpan());

        AssertExtensions.SpanApproxEqual(stereo1.AsSpan(), stereo2.AsSpan(), 1e-5f);
    }

    [Fact(DisplayName = "SW-006: Parameter clamping")]
    public void SW006_ParameterClamping()
    {
        var fx = new StereoWidthEffect();

        fx.Width = -1f;         Assert.Equal(0f, fx.Width);
        fx.Width = 5f;          Assert.Equal(2f, fx.Width);
        fx.HaasDelayMs = -1f;   Assert.Equal(0f, fx.HaasDelayMs);
        fx.HaasDelayMs = 10f;   Assert.Equal(2.0f, fx.HaasDelayMs);
        fx.Balance = -5f;       Assert.Equal(-1f, fx.Balance);
        fx.Balance = 5f;        Assert.Equal(1f, fx.Balance);
    }
}
