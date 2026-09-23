// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm regression for the triple-LFO chorus used by the Female preset.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Tests.Helpers;

public class ChorusEffectTests
{
    private const int SampleCount = 9600;

    [Fact]
    public void CH001_DefaultValues_Rate16_Depth03_Mix04()
    {
        var effect = new VxChorus();
        Assert.Equal(1.6f, effect.Rate, 0.001f);
        Assert.Equal(0.3f, effect.Depth, 0.001f);
        Assert.Equal(0.4f, effect.Mix, 0.001f);
    }

    [Fact]
    public void CH002_MixZero_AllDry_ApproxSpanEqual()
    {
        var effect = new VxChorus { Mix = 0f };
        var input = AudioTestHelpers.GenerateSineWave(1000f, SampleCount, 0.5f);
        var expected = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(expected.AsSpan(), buffer.AsSpan(), 1e-4f));
    }

    [Fact]
    public void CH003_Mix04_OutputDiffersFromInput_Over1PctRms()
    {
        var effect = new VxChorus { Mix = 0.4f };
        var input = AudioTestHelpers.GenerateSineWave(1000f, SampleCount, 0.5f);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), buffer.AsSpan());
        Assert.True(rmsDiff > 0.01f, $"RMS diff = {rmsDiff:P2}, expected > 1%");
    }

    [Fact]
    public void CH006_ResetTwice_ProcessResultsApproxEqual()
    {
        var effect = new VxChorus { Rate = 2.0f, Depth = 0.5f, Mix = 0.5f };
        var input = AudioTestHelpers.GenerateSineWave(440f, SampleCount, 0.5f);

        effect.Reset();
        var buf1 = (float[])input.Clone();
        effect.Process(buf1.AsSpan());

        effect.Reset();
        var buf2 = (float[])input.Clone();
        effect.Process(buf2.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-5f));
    }

    [Fact]
    public void CH008_DisableEffect_BypassSpanEqual()
    {
        var effect = new VxChorus { IsEnabled = false, Mix = 0.8f };
        var input = AudioTestHelpers.GenerateSineWave(1000f, SampleCount, 0.5f);
        var expected = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(effect);
        chain.Process(buffer.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), buffer.AsSpan()));
    }
}
