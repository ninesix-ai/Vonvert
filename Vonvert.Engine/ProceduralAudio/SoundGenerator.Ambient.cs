// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Ambient sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenWhiteNoise(int sr, float durSec)
    {
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(123);
        for (int i = 0; i < n; i++)
        {
            float env = FadeInOut(i, n, sr * 0.05f);
            buf[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.5f;
        }
        return buf;
    }

    private static float[] GenPinkNoise(int sr, float durSec)
    {
        // Voss-McCartney algorithm (simplified)
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(456);
        const int NumRows = 16;
        float[] rows = new float[NumRows];
        float runningSum = 0f;
        int counter = 0;
        for (int i = 0; i < n; i++)
        {
            // Update rows based on trailing zeros of counter
            int c = counter;
            int numZeros = 0;
            if (c == 0) { numZeros = NumRows; }
            else { while ((c & 1) == 0 && numZeros < NumRows) { numZeros++; c >>= 1; } }
            for (int r = 0; r < Math.Min(numZeros, NumRows); r++)
            {
                runningSum -= rows[r];
                rows[r] = (float)(rng.NextDouble() * 2.0 - 1.0);
                runningSum += rows[r];
            }
            float env = FadeInOut(i, n, sr * 0.05f);
            buf[i] = (runningSum / NumRows) * env * 0.5f;
            counter++;
        }
        return buf;
    }

    private static float[] GenBrownNoise(int sr, float durSec)
    {
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(789);
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            last = (last + 0.02f * white) / 1.02f; // leaky integrator
            float env = FadeInOut(i, n, sr * 0.05f);
            buf[i] = last * 3.5f * env * 0.5f; // scale up then attenuate
        }
        // Normalize
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    // ── NEW: DRUMS (expansion) ───────────────────────────────────────────

    private static float[] GenRain(int sr)
    {
        float durSec = 3.0f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(202);
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.18f * (white - lp);          // body of rain
            hp = white - lp;                      // patter hiss
            // random droplet ticks
            float drop = 0f;
            if (rng.NextDouble() > 0.9985)
            {
                float t = (float)i / sr;
                float f = 900f + (float)rng.NextDouble() * 1800f;
                drop = MathF.Sin(2f * MathF.PI * f * t) * 0.4f;
            }
            float env = FadeInOut(i, n, sr * 0.3f);
            buf[i] = (lp * 0.9f + hp * 0.35f + drop) * env * 0.5f;
        }
        return buf;
    }

    private static float[] GenThunder(int sr)
    {
        float durSec = 2.8f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(303);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            // double rumble envelope
            float env = ExpDecay(tMs, 800f, durSec * 1000f)
                      + 0.6f * ExpDecay(MathF.Max(0f, tMs - 700f), 900f, durSec * 1000f - 700f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.045f * (white - lp); // deep rumble
            float crack = white * (tMs < 150f ? 0.5f : 0.08f); // initial crack
            buf[i] = (lp * 2.6f + crack) * env * 0.65f;
        }
        return buf;
    }

    private static float[] GenOcean(int sr)
    {
        float durSec = 3.5f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(404);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            // slow wave swell (~0.35 Hz) shaping filtered noise
            float swell = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * 0.35f * t - MathF.PI / 2f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.06f * (white - lp);
            float env = FadeInOut(i, n, sr * 0.3f);
            buf[i] = lp * (0.3f + 1.4f * swell * swell) * env * 0.9f;
        }
        return buf;
    }

    private static float[] GenCampfire(int sr)
    {
        float durSec = 3.0f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(505);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.09f * (white - lp);
            // random crackles
            float crack = 0f;
            if (rng.NextDouble() > 0.996)
                crack = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.9f;
            float env = FadeInOut(i, n, sr * 0.2f);
            buf[i] = (lp * 1.1f + crack) * env * 0.6f;
        }
        return buf;
    }

    // ── Utility ─────────────────────────────────────────────────────────
}
