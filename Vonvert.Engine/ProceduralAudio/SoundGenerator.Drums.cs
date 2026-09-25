// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Drums sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenKick(int sr)
    {
        float durMs = 400f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            // Frequency slides from 150Hz down to 40Hz
            float freq = 40f + 110f * MathF.Exp(-tMs / 60f);
            phase += 2f * MathF.PI * freq / sr;
            float env = ExpDecay(tMs, 150f, durMs);
            buf[i] = MathF.Sin(phase) * env * 0.9f;
        }
        return buf;
    }

    private static float[] GenSnare(int sr)
    {
        float durMs = 200f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        var rng = new Random(42);
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float env = ExpDecay(tMs, 80f, durMs);
            // White noise + 200Hz triangle wave
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float tri = MathF.Abs(2f * (200f * t % 1f) - 1f) * 2f - 1f;
            buf[i] = (noise * 0.6f + tri * 0.4f) * env * 0.8f;
        }
        return buf;
    }

    private static float[] GenHiHat(int sr)
    {
        float durMs = 50f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        var rng = new Random(99);
        // Simple high-passed noise: difference of consecutive random values
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float cur = (float)(rng.NextDouble() * 2.0 - 1.0);
            float hp = cur - prev; // simple 1-zero HPF
            prev = cur;
            float env = ExpDecay(tMs, 15f, durMs);
            buf[i] = hp * env * 0.7f;
        }
        // Normalize to prevent clipping
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenClap(int sr)
    {
        float durMs = 250f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        var rng = new Random(77);
        int gapSamples = (int)(sr * 0.015f); // 15ms between bursts
        for (int burst = 0; burst < 4; burst++)
        {
            int offset = burst * gapSamples;
            int burstLen = Math.Min(gapSamples, n - offset);
            for (int j = 0; j < burstLen; j++)
            {
                int i = offset + j;
                if (i >= n) break;
                float tMs = (i - offset) * 1000f / sr;
                float env = ExpDecay(tMs, 20f, 40f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buf[i] += noise * env * 0.25f;
            }
        }
        // Tail: filtered noise decay
        int tailStart = 4 * gapSamples;
        for (int i = tailStart; i < n; i++)
        {
            float tMs = (i - tailStart) * 1000f / sr;
            float env = ExpDecay(tMs, 60f, durMs - tailStart * 1000f / sr);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            buf[i] += noise * env * 0.3f;
        }
        // Normalize
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    // ── TONES ────────────────────────────────────────────────────────────

    private static float[] GenTom(int sr)
    {
        float durMs = 350f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float freq = 90f + 80f * MathF.Exp(-tMs / 80f);
            phase += 2f * MathF.PI * freq / sr;
            float env = ExpDecay(tMs, 120f, durMs);
            buf[i] = MathF.Sin(phase) * env * 0.85f;
        }
        return buf;
    }

    private static float[] GenRimshot(int sr)
    {
        float durMs = 120f;
        int n = (int)(sr * durMs / 1000f);
        var buf = new float[n];
        var rng = new Random(31);
        // Sharp click: 1800Hz damped sine + noise transient
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            phase += 2f * MathF.PI * 1800f / sr;
            float tone = MathF.Sin(phase) * ExpDecay(tMs, 15f, durMs);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * ExpDecay(tMs, 8f, durMs) * 0.5f;
            buf[i] = (tone + noise) * 0.8f;
        }
        return buf;
    }

    private static float[] GenDrumroll(int sr)
    {
        float durSec = 1.2f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(63);
        // Rapid alternating snare hits (~18 Hz)
        int hitGap = (int)(sr / 18f);
        for (int hit = 0; hit * hitGap < n; hit++)
        {
            int start = hit * hitGap;
            int hitLen = Math.Min((int)(sr * 0.05f), n - start);
            for (int j = 0; j < hitLen; j++)
            {
                float tMs = j * 1000f / sr;
                float env = ExpDecay(tMs, 18f, 50f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buf[start + j] += noise * env * 0.45f;
            }
        }
        // Normalize
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenCrash(int sr)
    {
        float durSec = 1.4f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(88);
        // Metallic shimmer: multiple detuned square-ish carriers + noise
        float p1 = 0f, p2 = 0f, p3 = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float env = ExpDecay(tMs, 350f, durSec * 1000f);
            p1 += 2f * MathF.PI * 5143f / sr;
            p2 += 2f * MathF.PI * 7241f / sr;
            p3 += 2f * MathF.PI * 9673f / sr;
            float metal = (MathF.Sign(MathF.Sin(p1)) + MathF.Sign(MathF.Sin(p2)) + MathF.Sign(MathF.Sin(p3))) / 3f;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            buf[i] = (metal * 0.4f + noise * 0.6f) * env * 0.55f;
        }
        return buf;
    }

    private static float[] GenGong(int sr)
    {
        float durSec = 2.5f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        // Inharmonic partials with slow beating decay
        float[] partials = [98f, 147f, 233f, 331f, 451f];
        float[] amps    = [1.0f, 0.6f, 0.45f, 0.3f, 0.2f];
        float[] phases = new float[partials.Length];
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float env = ExpDecay(tMs, 900f, durSec * 1000f);
            float strike = ExpDecay(tMs, 20f, 200f) * 0.8f;
            float s = 0f;
            for (int p = 0; p < partials.Length; p++)
            {
                phases[p] += 2f * MathF.PI * partials[p] / sr;
                // slight amplitude modulation (beating)
                float beat = 1f + 0.15f * MathF.Sin(2f * MathF.PI * (1.7f + p * 0.9f) * t);
                s += MathF.Sin(phases[p]) * amps[p] * beat;
            }
            buf[i] = (s / partials.Length * env + strike) * 0.8f;
        }
        return buf;
    }

    // ── NEW: TONES ───────────────────────────────────────────────────────
}
