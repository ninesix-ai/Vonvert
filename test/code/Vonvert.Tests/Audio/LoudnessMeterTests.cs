// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  LoudnessMeter — LM-001/LM-002: RequestReset defers the destructive
//  Reset to the analyzer thread that owns the block lists, so a UI
//  click can never race FeedSamples on a shared List<float>.
// ═══════════════════════════════════════════════════════════════════

public sealed class LoudnessMeterTests
{
    private const int Rate = 48000;

    private static float[] Tone(float hz, int n)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = 0.5f * MathF.Sin(2f * MathF.PI * hz * i / Rate);
        return s;
    }

    [Fact(DisplayName = "LM-001: integrated loudness accumulates over 400 ms blocks")]
    public void LM001_IntegratedAccumulates()
    {
        var m = new LoudnessMeter();
        m.FeedSamples(Tone(1000f, Rate));   // 1 s of tone
        Assert.True(m.IntegratedLufs > -50f, $"expected accumulation, got {m.IntegratedLufs:F1}");
    }

    [Fact(DisplayName = "LM-002: RequestReset applies on the next FeedSamples, not on the requesting thread")]
    public void LM002_RequestResetAppliesOnFeed()
    {
        var m = new LoudnessMeter();
        m.FeedSamples(Tone(1000f, Rate));
        m.RequestReset();
        // The requesting thread must not see the state cleared synchronously.
        Assert.True(m.IntegratedLufs > -50f, "Reset ran eagerly on the requesting thread");
        m.FeedSamples(Tone(1000f, 480));    // one 10 ms block on the analyzer thread
        Assert.True(m.IntegratedLufs <= -50f, $"deferred reset not applied: {m.IntegratedLufs:F1}");
    }
}
