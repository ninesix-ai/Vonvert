// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.Synthesis;

/// <summary>
/// Pure-code speech-like signal generator.
/// Simulates vowel resonances + glottal pulse + natural intonation for audio effect
/// demonstrations and testing. Zero dependencies.
/// </summary>
public static class SpeechSynthesizer
{
    private const int DefaultSampleRate = AudioConstants.EngineRate;

    /// <summary>
    /// Generates a speech-like signal with pitch perturbation, 3 vocal-tract resonances,
    /// syllable envelope, vibrato, and breathiness noise.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default 48000).</param>
    /// <param name="durationSec">Duration in seconds (default 4.0).</param>
    /// <param name="baseFreq">Base fundamental frequency in Hz. Male ~120, Female ~220, Child ~300 (default 170).</param>
    /// <returns>float32 mono PCM samples (-1.0 .. 1.0).</returns>
    public static float[] Generate(
        int sampleRate = DefaultSampleRate,
        float durationSec = 4.0f,
        float baseFreq = 170f)
    {
        if (sampleRate <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
        if (durationSec <= 0) throw new ArgumentException("Duration must be positive.", nameof(durationSec));
        if (baseFreq <= 0) throw new ArgumentException("Base frequency must be positive.", nameof(baseFreq));

        int numSamples = (int)(sampleRate * durationSec);
        var buffer = new float[numSamples];

        // Vocal-tract resonance frequencies for vowel-like sound (R1, R2, R3)
        float[] resonances = { 700f, 1200f, 2600f };
        float[] resonanceAmps = { 1.0f, 0.6f, 0.3f };

        var rng = new Random(42);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;

            // Pitch variation (natural speech intonation)
            float pitchMod = 1.0f + 0.1f * MathF.Sin(2f * MathF.PI * 3f * t);
            float freq = baseFreq * pitchMod;

            // Generate glottal pulse (sawtooth-like for rich harmonics)
            float phase = (freq * t) % 1.0f;
            float glottal = 2f * phase - 1f;

            // Apply resonance filtering (simplified vocal-tract model)
            float sample = 0f;
            for (int f = 0; f < resonances.Length; f++)
            {
                float resPhase = (resonances[f] * t) % 1.0f;
                float band = MathF.Sin(2f * MathF.PI * resPhase);
                sample += band * resonanceAmps[f];
            }

            // Mix glottal source with the resonance bands
            sample = glottal * 0.3f + sample * 0.7f;

            // Amplitude envelope (natural speech syllables)
            float envFreq = 4f; // ~4 syllables per second
            float envelope = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * envFreq * t);
            envelope *= FadeInOut(t, durationSec, 0.1f);

            // Add slight vibrato
            float vibrato = 1.0f + 0.02f * MathF.Sin(2f * MathF.PI * 5f * t);

            // Add breathiness (noise component)
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.05f;

            buffer[i] = (sample * vibrato * envelope + noise) * 0.4f;
        }

        // Normalize to peak = 1.0
        float peak = 0f;
        for (int i = 0; i < numSamples; i++)
            peak = MathF.Max(peak, MathF.Abs(buffer[i]));

        if (peak > 0f)
            for (int i = 0; i < numSamples; i++)
                buffer[i] /= peak;

        return buffer;
    }

    /// <summary>
    /// Generates and converts to IEEE float32 PCM byte array.
    /// </summary>
    public static byte[] GenerateBytes(
        int sampleRate = DefaultSampleRate,
        float durationSec = 4.0f,
        float baseFreq = 170f)
    {
        var samples = Generate(sampleRate, durationSec, baseFreq);
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Common voice presets for quick generation.
    /// </summary>
    public static class Presets
    {
        /// <summary>Male voice (~120 Hz fundamental).</summary>
        public const float Male = 120f;

        /// <summary>Female voice (~220 Hz fundamental).</summary>
        public const float Female = 220f;

        /// <summary>Child voice (~300 Hz fundamental).</summary>
        public const float Child = 300f;
    }

    private static float FadeInOut(float t, float totalDuration, float fadeTime)
    {
        if (t < fadeTime) return t / fadeTime;
        if (t > totalDuration - fadeTime) return MathF.Max(0f, (totalDuration - t) / fadeTime);
        return 1.0f;
    }
}
