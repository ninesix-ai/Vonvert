// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

public class AdaptivePitchNormalizerTests
{
    private static AdaptivePitchNormalizer Ready(float target = 220f, float fallback = 4f)
        => new() { TargetF0Hz = target, FallbackSemitones = fallback, Enabled = true };

    private static void Feed(AdaptivePitchNormalizer n, bool voiced, float hz, int frames)
    {
        for (int i = 0; i < frames; i++) n.Update(voiced, hz);
    }

    [Fact]
    public void BeforeReady_UsesFallback_AndNoTarget()
    {
        var n = Ready();
        n.Update(true, 110f);            // single frame < ReadyMinFrames(10)
        Assert.False(n.HasTarget);
        Assert.Equal(4f, n.Semitones, 3);
    }

    [Fact]
    public void ConvergesToOneOctave_ClampedPlus12()
    {
        var n = Ready(target: 220f);
        Feed(n, true, 110f, 400);        // 12*log2(220/110)=+12 (clamped); settles within dead-band
        Assert.True(n.HasTarget);
        Assert.InRange((double)n.Semitones, 11.0, 12.0);
    }

    [Fact]
    public void ConvergesNear_180To220()
    {
        var n = Ready(target: 220f);
        Feed(n, true, 180f, 400);        // 12*log2(220/180)≈+3.47; settles within dead-band
        Assert.InRange((double)n.Semitones, 2.6, 3.5);
    }

    [Fact]
    public void Unvoiced_Holds_LastValue()
    {
        var n = Ready();
        Feed(n, true, 180f, 200);
        float before = n.Semitones;
        for (int i = 0; i < 50; i++) n.Update(false, 0f);
        Assert.Equal(before, n.Semitones, 3);
    }

    [Fact]
    public void RateLimit_MaxStepPerUpdate()
    {
        var n = Ready(target: 220f, fallback: 0f);
        float prev = n.Semitones, maxDelta = 0f;
        for (int i = 0; i < 60; i++)
        {
            n.Update(true, 110f);
            maxDelta = Math.Max(maxDelta, Math.Abs(n.Semitones - prev));
            prev = n.Semitones;
        }
        Assert.True(maxDelta <= 0.26f, $"single-step delta {maxDelta} must be <= 0.25 (+eps)");
    }

    [Fact]
    public void Disabled_DoesNotTakeOver()
    {
        var n = new AdaptivePitchNormalizer { TargetF0Hz = 220f, FallbackSemitones = 4f, Enabled = false };
        Feed(n, true, 110f, 200);
        Assert.False(n.HasTarget);
    }

    [Fact]
    public void Reset_ClearsTarget()
    {
        var n = Ready();
        Feed(n, true, 110f, 200);
        Assert.True(n.HasTarget);
        n.Reset();
        Assert.False(n.HasTarget);
    }
}
