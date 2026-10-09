// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.Synthesis;

/// <summary>
/// Optional effect processing interface.
/// Implement this to apply custom DSP effects (EQ, reverb, pitch shift, etc.)
/// to the generated speech signal via <see cref="SpeechSynthesizerExtensions.GenerateWithEffects"/>.
/// </summary>
public interface ISpeechEffect
{
    /// <summary>
    /// Processes the audio buffer in-place.
    /// </summary>
    /// <param name="buffer">Float32 PCM samples to process.</param>
    void Process(Span<float> buffer);
}
