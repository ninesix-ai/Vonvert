// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm regression for the feed-forward compressor (enabled by default in the built-in chain).

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Tests.Helpers;

public class CompressorEffectTests
{
    private const int SampleCount = 9600;

    [Fact]
    public void CP001_DefaultValues_ThreshMinus18_Ratio4()
    {
        var effect = new VxCompressor();
        Assert.Equal(-18f, effect.ThresholdDb, 0.001f);
        Assert.Equal(4f, effect.Ratio, 0.001f);
    }

    [Fact]
    public void CP002_QuietNoise_Amp001_BarelyCompressed()
    {
        var effect = new VxCompressor();
        var input = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.01f, 99);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsIn = AudioTestHelpers.ComputeRMS(original.AsSpan());
        float rmsOut = AudioTestHelpers.ComputeRMS(buffer.AsSpan());
        float ratio = rmsIn > 0 ? rmsOut / rmsIn : 0f;
        Assert.True(ratio > 0.95f, $"RMS ratio = {ratio:P2}, expected > 95% (near 1:1)");
    }

    [Fact]
    public void CP003_LoudSine_Amp05_PeakSignificantlyReduced()
    {
        var effect = new VxCompressor { ThresholdDb = -20f, Ratio = 8f };
        const int longCount = 48000;
        var input = AudioTestHelpers.GenerateSineWave(1000f, longCount, 0.8f);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        int tailStart = longCount / 2;
        var tailIn = new Span<float>(original, tailStart, longCount - tailStart);
        var tailOut = new Span<float>(buffer, tailStart, longCount - tailStart);
        float peakIn = AudioTestHelpers.ComputePeak(tailIn);
        float peakOut = AudioTestHelpers.ComputePeak(tailOut);
        Assert.True(peakOut < peakIn * 0.8f, $"Peak in={peakIn:F4}, out={peakOut:F4}, expected reduction to < 80%");
    }

    [Fact]
    public void CP004_RatioEqualsOne_NoCompression_ApproxOriginal()
    {
        var effect = new VxCompressor { Ratio = 1f, ThresholdDb = -40f };
        var input = AudioTestHelpers.GenerateSineWave(440f, SampleCount, 0.5f);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buffer.AsSpan());
        Assert.True(rmsDiff < 0.05f, $"RMS diff = {rmsDiff:P2}, expected < 5%");
    }

    [Fact]
    public void CP006_Reset_NoCrash()
    {
        var effect = new VxCompressor();
        var buffer = AudioTestHelpers.GenerateSineWave(440f, SampleCount, 0.5f);

        effect.Process(buffer.AsSpan());
        var ex = Record.Exception(() => effect.Reset());
        Assert.Null(ex);

        buffer = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.3f, 7);
        ex = Record.Exception(() => effect.Process(buffer.AsSpan()));
        Assert.Null(ex);
    }

    [Fact]
    public void CP007_CreateDefault_CompressorIsEnabled()
    {
        var chain = DSPChain.CreateDefault();
        var compressor = chain.Effects.OfType<VxCompressor>().FirstOrDefault();
        Assert.NotNull(compressor);
        Assert.True(compressor!.IsEnabled);
    }
}
