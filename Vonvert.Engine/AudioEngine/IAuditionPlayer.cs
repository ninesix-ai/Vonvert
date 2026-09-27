// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.Engine.AudioEngine;

/// <summary>A device-independent one-shot audition output channel.</summary>
public interface IAuditionPlayer
{
    /// <summary>Queue float32 mono PCM (at <see cref="AudioConstants.EngineRate"/>) for local
    /// playback. Returns a tracking token (0 when the payload was ignored); pass the pad's
    /// <paramref name="soundId"/> so re-triggers replace that pad's previous voice.</summary>
    long Play(byte[] pcm, float volume, string? soundId = null);

    /// <summary>Read-only playback progress for a token; false once finished or unknown.</summary>
    bool TryGetProgress(long token, out int position, out int length);
}

/// <summary>Pure idle decision: has a sink been silent long enough to release the device?</summary>
public static class IdleDecider
{
    public static bool ShouldStop(int silentTicks, int stopAfterTicks) => silentTicks >= stopAfterTicks;
}
