// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Single source of truth for audio-pipeline constants shared across the
/// engine and every audio consumer, so the WAV header rate always matches
/// the actual DSP chain rate.
/// </summary>
public static class AudioConstants
{
    /// <summary>Engine DSP rate used by the whole audio graph (Hz).</summary>
    public const int EngineRate = 48000;
}
