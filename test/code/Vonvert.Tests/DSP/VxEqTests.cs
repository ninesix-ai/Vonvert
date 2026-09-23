// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm regression for the three-band parametric EQ used by the built-in presets.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

public class EQEffectTests
{
    private const int SampleCount = 48000;

    [Fact]
    public void EQ001_DefaultGains_AllZeroDb()
    {
        var effect = new VxEq();
        Assert.Equal(0f, effect.LowGainDb, 0.001f);
        Assert.Equal(0f, effect.MidGainDb, 0.001f);
        Assert.Equal(0f, effect.HighGainDb, 0.001f);
    }

    [Fact]
    public void EQ002_DefaultZeroDb_OutputApproxInput()
    {
        var effect = new VxEq();
        var input = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.3f, 1);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buffer.AsSpan());
        Assert.True(rmsDiff < 0.10f, $"RMS diff = {rmsDiff:P2}, expected < 10%");
    }

    [Fact]
    public void EQ003_LowPlus12Db_LowBandEnergyDoubled()
    {
        var effect = new VxEq { LowGainDb = 12f, HighGainDb = 0f, MidGainDb = 0f };
        var sweep = AudioTestHelpers.GenerateSweep(SampleCount, 50f, 1000f, 0.5f);
        int half = SampleCount / 2;
        var inLow = new Span<float>(sweep, 0, half);
        var outBuf = (float[])sweep.Clone();

        effect.Process(outBuf.AsSpan());
        var outLow = new Span<float>(outBuf, 0, half);

        double eInLow = AudioTestHelpers.ComputeEnergy(inLow);
        double eOutLow = AudioTestHelpers.ComputeEnergy(outLow);
        double ratio = eInLow > 0 ? eOutLow / eInLow : 0;
        Assert.True(ratio > 2.0, $"Low band energy ratio = {ratio:F2}, expected > 2x");
    }

    [Fact]
    public void EQ004_HighPlus12Db_HighBandEnergyDoubled()
    {
        var effect = new VxEq { HighGainDb = 12f, LowGainDb = 0f, MidGainDb = 0f };
        var sweep = AudioTestHelpers.GenerateSweep(SampleCount, 1000f, 20000f, 0.5f);
        int half = SampleCount / 2;
        var outBuf = (float[])sweep.Clone();

        effect.Process(outBuf.AsSpan());
        var outHigh = new Span<float>(outBuf, half, SampleCount - half);
        var inHigh = new Span<float>(sweep, half, SampleCount - half);

        double eInHigh = AudioTestHelpers.ComputeEnergy(inHigh);
        double eOutHigh = AudioTestHelpers.ComputeEnergy(outHigh);
        double ratio = eInHigh > 0 ? eOutHigh / eInHigh : 0;
        Assert.True(ratio > 2.0, $"High band energy ratio = {ratio:F2}, expected > 2x");
    }

    [Fact]
    public void EQ007_UpdateParamsAndReset_EffectApplies()
    {
        var effect = new VxEq();
        var input = AudioTestHelpers.GenerateSweep(SampleCount, 50f, 1000f, 0.5f);
        var before = new float[SampleCount];
        Array.Copy(input, before, SampleCount);
        effect.Process(before.AsSpan());

        effect.UpdateParams(12f, 0f, 0f);
        effect.Reset();

        var after = (float[])input.Clone();
        effect.Process(after.AsSpan());

        int half = SampleCount / 2;
        double eBefore = AudioTestHelpers.ComputeEnergy(new Span<float>(before, 0, half));
        double eAfter = AudioTestHelpers.ComputeEnergy(new Span<float>(after, 0, half));
        double ratio = eBefore > 0 ? eAfter / eBefore : 0;
        Assert.True(ratio > 1.5, $"After UpdateParams+Reset, low band ratio = {ratio:F2}, expected > 1.5x");
    }

    [Fact]
    public void EQ008_DisableEffect_BypassSpanEqual()
    {
        var effect = new VxEq { IsEnabled = false, LowGainDb = 12f, HighGainDb = -6f };
        var input = AudioTestHelpers.GenerateSineWave(1000f, 4800, 0.5f);
        var expected = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(effect);
        chain.Process(buffer.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), buffer.AsSpan()));
    }
}
