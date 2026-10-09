// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Engine.Synthesis;

namespace Vonvert.Engine.Soundboard;

/// <summary>
/// Generates preview audio samples for voice presets by applying DSP effects
/// to a base audio signal (speech-like synthesis or imported audio).
/// </summary>
public static class PresetPreviewGenerator
{
    private const int SAMPLE_RATE = AudioConstants.EngineRate;
    private const float PREVIEW_DURATION_SEC = 4.0f;

    /// <summary>
    /// Generates a preview audio clip for a voice preset by applying its DSP chain
    /// to a synthesized speech-like signal.
    /// </summary>
    /// <param name="preset">The voice preset to preview</param>
    /// <returns>Float32 PCM samples at 48kHz mono</returns>
    public static float[] GeneratePreview(VoiceProfile preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        // Generate base audio: speech-like signal (vowel-rich)
        var baseAudio = SpeechSynthesizer.Generate(SAMPLE_RATE, PREVIEW_DURATION_SEC);

        // Create DSP chain and apply preset settings
        var chain = preset.CreateDSPChain();

        // Process the audio through the chain
        var samples = new Span<float>(baseAudio);
        chain.Process(samples);

        return baseAudio;
    }

    /// <summary>
    /// Generates preview audio as bytes (IEEE float32 PCM).
    /// </summary>
    public static byte[] GeneratePreviewBytes(VoiceProfile preset)
    {
        var samples = GeneratePreview(preset);
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
