// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Shared signal-generation and analysis helpers for audio tests.

namespace Vonvert.Tests.Helpers;

public static class AudioTestHelpers
{
    private const float SampleRate = 48000f;

    public static float[] GenerateSineWave(float frequencyHz, int sampleCount, float amplitude = 0.5f, float phase = 0f)
    {
        float[] result = new float[sampleCount];
        float phaseInc = MathF.Tau * frequencyHz / SampleRate;
        float currentPhase = phase;
        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = amplitude * MathF.Sin(currentPhase);
            currentPhase += phaseInc;
            if (currentPhase > MathF.Tau) currentPhase -= MathF.Tau;
        }
        return result;
    }

    public static float[] GenerateSine(float freqHz, int samples, float amp = 0.5f, float phase = 0f)
        => GenerateSineWave(freqHz, samples, amp, phase);

    public static float[] GenerateWhiteNoise(int sampleCount, float amplitude = 0.5f, int seed = 42)
    {
        Random rng = new Random(seed);
        float[] result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * amplitude;
        }
        return result;
    }

    public static float[] GenerateSweep(int sampleCount, float startFreqHz, float endFreqHz, float amplitude = 0.5f)
    {
        float[] result = new float[sampleCount];
        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float freq = startFreqHz + (endFreqHz - startFreqHz) * t;
            float phaseInc = MathF.Tau * freq / SampleRate;
            result[i] = amplitude * MathF.Sin(phase);
            phase += phaseInc;
            if (phase > MathF.Tau) phase -= MathF.Tau;
        }
        return result;
    }

    public static float[] GenerateImpulse(int sampleCount, int impulsePosition = 0, float amplitude = 1f)
    {
        float[] result = new float[sampleCount];
        if (impulsePosition >= 0 && impulsePosition < sampleCount)
            result[impulsePosition] = amplitude;
        return result;
    }

    public static float ComputeRMS(Span<float> buffer)
    {
        if (buffer.Length == 0) return 0f;
        double sum = 0.0;
        for (int i = 0; i < buffer.Length; i++)
        {
            sum += (double)buffer[i] * buffer[i];
        }
        return (float)Math.Sqrt(sum / buffer.Length);
    }

    public static double ComputeRms(ReadOnlySpan<float> buffer)
    {
        if (buffer.Length == 0) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < buffer.Length; i++)
        {
            sum += (double)buffer[i] * buffer[i];
        }
        return Math.Sqrt(sum / buffer.Length);
    }

    public static double ComputeRms(float[] buffer)
        => ComputeRms(buffer.AsSpan());

    public static double ComputeEnergy(Span<float> buffer)
    {
        double sum = 0.0;
        for (int i = 0; i < buffer.Length; i++)
        {
            sum += (double)buffer[i] * buffer[i];
        }
        return sum;
    }

    public static int CountNonZero(Span<float> buffer, float epsilon = 1e-9f)
    {
        int count = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (MathF.Abs(buffer[i]) > epsilon) count++;
        }
        return count;
    }

    public static int CountClipped(Span<float> buffer, float threshold = 0.99f)
    {
        int count = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (MathF.Abs(buffer[i]) >= threshold) count++;
        }
        return count;
    }

    public static bool SpanEqual(Span<float> a, Span<float> b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    public static bool SpansApproxEqual(Span<float> a, Span<float> b, float tolerance = 1e-6f)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (MathF.Abs(a[i] - b[i]) > tolerance) return false;
        return true;
    }

    public static float ComputeRmsDifference(Span<float> a, Span<float> b)
    {
        if (a.Length != b.Length) return 1f;
        double sumSqDiff = 0.0;
        double sumSqA = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sumSqDiff += (double)diff * diff;
            sumSqA += (double)a[i] * a[i];
        }
        if (sumSqA < 1e-20) return (float)Math.Sqrt(sumSqDiff / Math.Max(1, a.Length));
        return (float)Math.Sqrt(sumSqDiff / sumSqA);
    }

    public static float ComputePeak(Span<float> buffer)
    {
        float peak = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            float abs = MathF.Abs(buffer[i]);
            if (abs > peak) peak = abs;
        }
        return peak;
    }

    /// <summary>Voice-like test signal: fundamental + two harmonic overtones
    /// with a slow amplitude envelope (roughly models voiced speech).</summary>
    public static float[] GenerateVoiceLike(float freqHz, int samples, float amp = 0.4f)
    {
        float[] result = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            // Fundamental + 2nd/3rd harmonic at lower amplitude (vowel-ish timbre)
            float s = MathF.Sin(2f * MathF.PI * freqHz * t)
                    + 0.5f * MathF.Sin(2f * MathF.PI * 2f * freqHz * t)
                    + 0.25f * MathF.Sin(2f * MathF.PI * 3f * freqHz * t);
            // 4-Hz syllable-like envelope
            float env = 0.75f + 0.25f * MathF.Sin(2f * MathF.PI * 4f * t);
            result[i] = amp * env * s;
        }
        return result;
    }

    public static double EstimateDominantFrequency(Span<float> buffer, float sampleRate = 48000f)
    {
        if (buffer.Length < 2) return 0.0;
        int zeroCrossings = 0;
        for (int i = 1; i < buffer.Length; i++)
        {
            if ((buffer[i - 1] >= 0f && buffer[i] < 0f) || (buffer[i - 1] < 0f && buffer[i] >= 0f))
                zeroCrossings++;
        }
        float duration = buffer.Length / sampleRate;
        return (zeroCrossings / 2.0) / Math.Max(duration, 1e-6f);
    }

    public static double EstimateDominantFrequency(float[] buffer, float sampleRate = 48000f)
        => EstimateDominantFrequency(buffer.AsSpan(), sampleRate);
}