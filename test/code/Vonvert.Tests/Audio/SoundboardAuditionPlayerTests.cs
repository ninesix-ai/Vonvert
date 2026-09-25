// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.Audio;

using System;
using Vonvert.Engine.AudioEngine;
using Xunit;

public sealed class SoundboardAuditionPlayerTests
{
    [Theory(DisplayName = "AUD-IDLE-001: stop only once silence ticks reach threshold")]
    [InlineData(0, 4, false)]
    [InlineData(3, 4, false)]
    [InlineData(4, 4, true)]
    [InlineData(9, 4, true)]
    public void IdleDecider_Threshold(int ticks, int stopAfter, bool expected)
        => Assert.Equal(expected, IdleDecider.ShouldStop(ticks, stopAfter));

    private sealed class FakeSink : IAudioSink
    {
        public int StartCount, StopCount, DisposeCount;
        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Dispose() => DisposeCount++;
    }

    [Fact(DisplayName = "AUD-001: Play enqueues a voice and lazily starts the sink once")]
    public void Play_StartsSinkOnce()
    {
        var sink = new FakeSink();
        using var p = new SoundboardAuditionPlayer(_ => sink);
        var pcm = new byte[480 * 4];
        p.Play(pcm, 1f);
        p.Play(pcm, 1f);
        Assert.Equal(2, p.ActiveVoices);
        Assert.Equal(1, sink.StartCount);   // started lazily, only once
    }

    [Fact(DisplayName = "AUD-002: sink factory failure degrades silently - no throw, still enqueues")]
    public void SinkFailure_NoThrow()
    {
        using var p = new SoundboardAuditionPlayer(_ => throw new InvalidOperationException("no device"));
        var ex = Record.Exception(() => p.Play(new byte[480 * 4], 1f));
        Assert.Null(ex);
        Assert.Equal(1, p.ActiveVoices);    // audio still buffered for a later retry
    }

    [Fact(DisplayName = "AUD-003: empty payload is ignored")]
    public void EmptyIgnored()
    {
        var sink = new FakeSink();
        using var p = new SoundboardAuditionPlayer(_ => sink);
        p.Play(Array.Empty<byte>(), 1f);
        Assert.Equal(0, p.ActiveVoices);
        Assert.Equal(0, sink.StartCount);
    }

    [Fact(DisplayName = "AUD-004: Dispose stops and releases the sink")]
    public void Dispose_StopsSink()
    {
        var sink = new FakeSink();
        var p = new SoundboardAuditionPlayer(_ => sink);
        p.Play(new byte[480 * 4], 1f);
        p.Dispose();
        Assert.Equal(1, sink.StopCount);
        Assert.Equal(1, sink.DisposeCount);
    }
}
