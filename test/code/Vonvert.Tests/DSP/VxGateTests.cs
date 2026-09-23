// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Algorithm regression for the hysteresis noise gate (enabled by default in the built-in chain).

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Tests.Helpers;

public class NoiseGateEffectTests
{
    private const int SampleCount = 9600;

    [Fact]
    public void NG001_DefaultValues_ThresholdMinus45_Attack2_Release100()
    {
        var effect = new VxGate();
        Assert.Equal(-45f, effect.ThresholdDb, 0.001f);
        Assert.Equal(2f, effect.AttackMs, 0.001f);
        Assert.Equal(100f, effect.ReleaseMs, 0.001f);
    }

    [Fact]
    public void NG002_BelowThreshold_SignalSuppressed()
    {
        var effect = new VxGate { ThresholdDb = -20f };
        float amp = 1e-5f;
        var input = AudioTestHelpers.GenerateWhiteNoise(SampleCount, amp, 321);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsIn = AudioTestHelpers.ComputeRMS(original.AsSpan());
        float rmsOut = AudioTestHelpers.ComputeRMS(buffer.AsSpan());
        float ratio = rmsIn > 0 ? rmsOut / rmsIn : 0f;
        Assert.True(ratio < 0.5f, $"RMS ratio = {ratio:P2}, expected < 50% (suppressed)");
    }

    [Fact]
    public void NG003_AboveThreshold_Amp05_PassesThrough()
    {
        var effect = new VxGate { ThresholdDb = -45f };
        var input = AudioTestHelpers.GenerateSineWave(1000f, SampleCount, 0.5f);
        var original = (float[])input.Clone();
        var buffer = (float[])input.Clone();

        effect.Process(buffer.AsSpan());

        float rmsIn = AudioTestHelpers.ComputeRMS(original.AsSpan());
        float rmsOut = AudioTestHelpers.ComputeRMS(buffer.AsSpan());
        float ratio = rmsIn > 0 ? rmsOut / rmsIn : 0f;
        Assert.True(ratio > 0.9f, $"RMS ratio = {ratio:P2}, expected > 90% (pass-through)");
    }

    [Fact]
    public void NG005_SilenceIn_SilenceOut()
    {
        var effect = new VxGate();
        var buffer = new float[SampleCount];

        effect.Process(buffer.AsSpan());

        for (int i = 0; i < SampleCount; i++)
            Assert.Equal(0f, buffer[i], 1e-9f);
    }

    [Fact]
    public void NG006_Reset_NoCrash()
    {
        var effect = new VxGate();
        var buffer = AudioTestHelpers.GenerateSineWave(440f, SampleCount, 0.5f);

        effect.Process(buffer.AsSpan());
        var ex = Record.Exception(() => effect.Reset());
        Assert.Null(ex);

        buffer = AudioTestHelpers.GenerateWhiteNoise(SampleCount, 0.2f, 11);
        ex = Record.Exception(() => effect.Process(buffer.AsSpan()));
        Assert.Null(ex);
    }

    [Fact]
    public void NG007_CreateDefault_NoiseGateIsEnabled()
    {
        var chain = DSPChain.CreateDefault();
        var noiseGate = chain.Effects.OfType<VxGate>().FirstOrDefault();
        Assert.NotNull(noiseGate);
        Assert.True(noiseGate!.IsEnabled);
    }
}
