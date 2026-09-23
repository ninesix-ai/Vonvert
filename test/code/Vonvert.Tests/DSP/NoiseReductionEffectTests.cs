// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// In this build the noise reducer is a pass-through stub, so the assertions
// pin the public parameter API, clamping, name, and the
// pass-through/no-learn behaviour.

namespace Vonvert.Tests.DSP;

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;

public class NoiseReductionEffectTests
{
    [Fact(DisplayName = "NR-001: Default Strength=2.0, Floor=0.01, LearnFrames=30")]
    public void NR001_DefaultParameters()
    {
        var effect = new NoiseReductionEffect();
        Assert.Equal(2.0f, effect.Strength);
        Assert.Equal(0.01f, effect.Floor);
        Assert.Equal(30, effect.LearnFrames);
        Assert.False(effect.ProfileReady);
    }

    [Fact(DisplayName = "NR-002: Strength clamped to [1, 4]")]
    public void NR002_StrengthClamped()
    {
        var effect = new NoiseReductionEffect();
        effect.Strength = 0.5f; Assert.Equal(1f, effect.Strength);
        effect.Strength = 5.0f; Assert.Equal(4f, effect.Strength);
        effect.Strength = 2.5f; Assert.Equal(2.5f, effect.Strength);
    }

    [Fact(DisplayName = "NR-003: Floor clamped to [0.001, 0.1]")]
    public void NR003_FloorClamped()
    {
        var effect = new NoiseReductionEffect();
        effect.Floor = 0.0001f; Assert.Equal(0.001f, effect.Floor);
        effect.Floor = 0.5f;    Assert.Equal(0.1f, effect.Floor);
        effect.Floor = 0.05f;   Assert.Equal(0.05f, effect.Floor);
    }

    [Fact(DisplayName = "NR-004: LearnFrames clamped to [10, 100]")]
    public void NR004_LearnFramesClamped()
    {
        var effect = new NoiseReductionEffect();
        effect.LearnFrames = 5;   Assert.Equal(10, effect.LearnFrames);
        effect.LearnFrames = 200; Assert.Equal(100, effect.LearnFrames);
        effect.LearnFrames = 50;  Assert.Equal(50, effect.LearnFrames);
    }

    [Fact(DisplayName = "NR-005: IsEnabled default false")]
    public void NR005_DefaultDisabled()
    {
        Assert.False(new NoiseReductionEffect().IsEnabled);
    }

    [Fact(DisplayName = "NR-006: Process produces finite output")]
    public void NR006_ProcessProducesFiniteOutput()
    {
        var effect = new NoiseReductionEffect { IsEnabled = true };
        var buffer = AudioTestHelpers.GenerateWhiteNoise(4800, 0.3f, 1);

        effect.Process(buffer.AsSpan());

        for (int i = 0; i < buffer.Length; i++)
            Assert.True(float.IsFinite(buffer[i]), $"Sample {i} is not finite: {buffer[i]}");
    }

    [Fact(DisplayName = "NR-007: Pass-through stub never learns")]
    public void NR007_StubNeverLearns()
    {
        var effect = new NoiseReductionEffect { IsEnabled = true, LearnFrames = 5 };
        Assert.Equal(0, effect.LearnProgress);
        Assert.False(effect.ProfileReady);

        for (int i = 0; i < 10; i++)
        {
            var chunk = AudioTestHelpers.GenerateWhiteNoise(256, 0.1f, 1);
            effect.Process(chunk.AsSpan());
        }

        Assert.Equal(0, effect.LearnProgress);
        Assert.False(effect.ProfileReady);
    }

    [Fact(DisplayName = "NR-008: Reset() leaves the stub idle")]
    public void NR008_ResetKeepsStubIdle()
    {
        var effect = new NoiseReductionEffect { IsEnabled = true, LearnFrames = 3 };
        for (int i = 0; i < 10; i++)
            effect.Process(AudioTestHelpers.GenerateWhiteNoise(256, 0.1f, 1).AsSpan());

        effect.Reset();
        Assert.False(effect.ProfileReady);
        Assert.Equal(0, effect.LearnProgress);
    }

    [Fact(DisplayName = "NR-009: StartLearning() is a safe no-op on the stub")]
    public void NR009_StartLearningIsSafe()
    {
        var effect = new NoiseReductionEffect { IsEnabled = true, LearnFrames = 3 };
        for (int i = 0; i < 10; i++)
            effect.Process(AudioTestHelpers.GenerateWhiteNoise(256, 0.1f, 1).AsSpan());

        effect.StartLearning();
        Assert.False(effect.ProfileReady);
        Assert.Equal(0, effect.LearnProgress);
    }

    [Fact(DisplayName = "NR-010: Name is 'VoiceNoiseReduce'")]
    public void NR010_NameIsCorrect()
    {
        Assert.Equal("VoiceNoiseReduce", new NoiseReductionEffect().Name);
    }
}
