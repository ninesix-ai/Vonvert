// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.Synthesis;

/// <summary>
/// Extension methods for applying custom effects to synthesized speech.
/// </summary>
public static class SpeechSynthesizerExtensions
{
    /// <summary>
    /// Generates a speech-like signal and passes it through a chain of custom effects.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="durationSec">Duration in seconds.</param>
    /// <param name="baseFreq">Base fundamental frequency in Hz.</param>
    /// <param name="effects">Effects to apply in order (each processes the buffer in-place).</param>
    /// <returns>Processed float32 PCM samples.</returns>
    public static float[] GenerateWithEffects(
        int sampleRate = AudioConstants.EngineRate,
        float durationSec = 4.0f,
        float baseFreq = 170f,
        params ISpeechEffect[] effects)
    {
        var samples = SpeechSynthesizer.Generate(sampleRate, durationSec, baseFreq);
        var span = samples.AsSpan();
        foreach (var effect in effects)
            effect.Process(span);
        return samples;
    }
}
