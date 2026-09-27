// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression for the soundboard one-shot mixer: additive injection, cursor advance,
// eviction when a clip finishes, headroom clamping, and Clear().

namespace Vonvert.Tests.DSP;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Vonvert.Engine.AudioEngine;

public sealed class SoundboardMixerTests
{
    private static byte[] Pcm(float[] samples) => MemoryMarshal.Cast<float, byte>(samples).ToArray();

    [Fact(DisplayName = "SBM-001: injected clip is added to the work buffer and evicted when it ends")]
    public void SBM001_InjectsThenFinishes()
    {
        var mix = new SoundboardMixer();
        var pcm = Pcm(Enumerable.Repeat(0.5f, 480).ToArray());   // 10 ms @ 48 kHz mono f32
        mix.Enqueue(pcm, 1.0f);

        var first = new float[128];
        mix.MixInto(first);
        Assert.Contains(first, v => MathF.Abs(v) > 0.1f);        // audible on frame 1

        float[] tail = new float[128];
        for (int i = 0; i < 40; i++) { tail = new float[128]; mix.MixInto(tail); }
        Assert.DoesNotContain(tail, v => MathF.Abs(v) > 0.0001f); // silent once finished
        Assert.Equal(0, mix.ActiveVoices);
    }

    [Fact(DisplayName = "SBM-002: overlapping clips clamp the summed output to [-1, 1]")]
    public void SBM002_ClampsSummedOutput()
    {
        var mix = new SoundboardMixer();
        var loud = Pcm(Enumerable.Repeat(0.8f, 256).ToArray());
        mix.Enqueue(loud, 1.0f);
        mix.Enqueue(loud, 1.0f);

        var work = new float[128];
        mix.MixInto(work);
        Assert.All(work, v => Assert.InRange(v, -1f, 1f));        // 0.8+0.8 would exceed 1 → clamped
        Assert.Contains(work, v => MathF.Abs(v - 1f) < 0.001f);
    }

    [Fact(DisplayName = "SBM-003: Clear() drops pending playback immediately")]
    public void SBM003_ClearStopsPlayback()
    {
        var mix = new SoundboardMixer();
        mix.Enqueue(Pcm(Enumerable.Repeat(0.5f, 48000).ToArray()), 1.0f);
        Assert.Equal(1, mix.ActiveVoices);
        mix.Clear();
        var work = new float[128];
        mix.MixInto(work);
        Assert.Equal(0, mix.ActiveVoices);
        Assert.DoesNotContain(work, v => MathF.Abs(v) > 0.0001f);
    }

    [Fact(DisplayName = "SBM-004: Enqueue returns fresh tokens; progress tracks pos and length")]
    public void SBM004_TokensAndProgress()
    {
        var mix = new SoundboardMixer();
        var pcm = Pcm(Enumerable.Repeat(0.5f, 256).ToArray());
        long t1 = mix.Enqueue(pcm, 1.0f, "kick");
        long t2 = mix.Enqueue(pcm, 1.0f, "snare");
        Assert.NotEqual(t1, t2);

        var work = new float[64];
        mix.MixInto(work);
        Assert.True(mix.TryGetProgress(t1, out int pos, out int len));
        Assert.Equal(64, pos);
        Assert.Equal(256, len);
    }

    [Fact(DisplayName = "SBM-005: re-trigger same soundId evicts the old voice and its token")]
    public void SBM005_SameIdRetriggerEvictsOldToken()
    {
        var mix = new SoundboardMixer();
        var longPcm = Pcm(Enumerable.Repeat(0.5f, 48000).ToArray());
        long oldTok = mix.Enqueue(longPcm, 1.0f, "kick");
        mix.MixInto(new float[64]);                       // old voice advances
        long newTok = mix.Enqueue(longPcm, 1.0f, "kick"); // replaces it
        Assert.NotEqual(oldTok, newTok);
        Assert.False(mix.TryGetProgress(oldTok, out _, out _));
        Assert.True(mix.TryGetProgress(newTok, out int pos, out _));
        Assert.Equal(1, mix.ActiveVoices);
        Assert.Equal(0, pos);                             // cursor restarted
    }

    [Fact(DisplayName = "SBM-006: finished or unknown tokens report no progress")]
    public void SBM006_FinishedTokenDies()
    {
        var mix = new SoundboardMixer();
        long tok = mix.Enqueue(Pcm(Enumerable.Repeat(0.5f, 128).ToArray()), 1.0f, "tick");
        mix.MixInto(new float[128]);                      // exactly one clip length → done
        Assert.False(mix.TryGetProgress(tok, out _, out _));
        Assert.False(mix.TryGetProgress(999999, out _, out _));
        Assert.Equal(0, mix.ActiveVoices);
    }
}
