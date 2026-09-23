// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm regression for the look-ahead brick-wall limiter (enabled by default in the built-in chain).

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Tests.Helpers;

public class LimiterEffectTests
{
    private const int SampleCount = 4800;

    [Fact]
    public void LM001_DefaultCeiling_Minus03Db()
    {
        var effect = new VxLimiter();
        Assert.Equal(-0.3f, effect.CeilingDb, 0.001f);
    }

    [Fact]
    public void LM002_ExtremeValues_AllWithinMinusOneAndOne()
    {
        var effect = new VxLimiter();
        var buffer = new float[SampleCount];
        for (int i = 0; i < SampleCount; i++)
            buffer[i] = (i % 2 == 0) ? 5.0f : -5.0f;

        effect.Process(buffer.AsSpan());

        for (int i = 0; i < buffer.Length; i++)
            Assert.True(buffer[i] >= -1.0001f && buffer[i] <= 1.0001f,
                $"Sample[{i}] = {buffer[i]} out of range [-1,1]");
    }

    [Fact]
    public void LM003_CeilingMinus20Db_PeakUnder0101()
    {
        var effect = new VxLimiter { CeilingDb = -20f };
        float ceilLinear = MathF.Pow(10f, -20f / 20f);
        const int longCount = 48000;
        var input = AudioTestHelpers.GenerateSineWave(1000f, longCount, 0.9f);
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        int tailStart = longCount / 2;
        var tail = new Span<float>(buffer, tailStart, longCount - tailStart);
        float peak = AudioTestHelpers.ComputePeak(tail);
        float limit = ceilLinear * 1.05f + 1e-3f;
        Assert.True(peak <= limit,
            $"Peak = {peak:F6}, expected <= {limit:F6} (Ceiling=-20dB, ceilLinear={ceilLinear:F6})");
    }

    [Fact]
    public void LM005_SmallSignal_Amp001_NearlyPassThrough()
    {
        var effect = new VxLimiter();
        var input = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.01f, 55);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buffer.AsSpan());
        Assert.True(rmsDiff < 0.05f, $"RMS diff = {rmsDiff:P2}, expected < 5%");
    }

    [Fact]
    public void LM006_Reset_NoCrash()
    {
        var effect = new VxLimiter();
        var buffer = AudioTestHelpers.GenerateSineWave(440f, SampleCount, 0.5f);

        effect.Process(buffer.AsSpan());
        var ex = Record.Exception(() => effect.Reset());
        Assert.Null(ex);

        buffer = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.3f, 88);
        ex = Record.Exception(() => effect.Process(buffer.AsSpan()));
        Assert.Null(ex);
    }

    [Fact]
    public void LM007_CreateDefault_LimiterSecondToLast()
    {
        var chain = DSPChain.CreateDefault();
        Assert.True(chain.Effects.Count > 2);
        // Default chain tail order: …Compressor → Limiter → LoudnessMeter,
        // so the limiter is the second-to-last effect.
        var limiter = chain.Effects[chain.Effects.Count - 2];
        Assert.IsType<VxLimiter>(limiter);
        Assert.True(limiter.IsEnabled);
    }
}
