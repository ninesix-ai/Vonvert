// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.Engine.AudioEngine;

/// <summary>A device-independent one-shot audition output channel.</summary>
public interface IAuditionPlayer
{
    /// <summary>Queue float32 mono PCM (at <see cref="AudioConstants.EngineRate"/>) for local playback.</summary>
    void Play(byte[] pcm, float volume);
}

/// <summary>Pure idle decision: has a sink been silent long enough to release the device?</summary>
public static class IdleDecider
{
    public static bool ShouldStop(int silentTicks, int stopAfterTicks) => silentTicks >= stopAfterTicks;
}
