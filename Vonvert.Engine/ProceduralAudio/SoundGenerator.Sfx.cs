// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Sfx sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenSweep(int sr, float f0, float f1, float durSec)
    {
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            float freq = f0 + (f1 - f0) * progress;
            phase += 2f * MathF.PI * freq / sr;
            float env = progress < 0.5f
                ? progress * 2f           // fade in
                : (1f - progress) * 2f;   // fade out
            buf[i] = MathF.Sin(phase) * env * 0.7f;
        }
        return buf;
    }

    private static float[] GenLaser(int sr)
    {
        float durSec = 0.15f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            // Exponential frequency drop 3000Hz -> 100Hz
            float freq = 100f + 2900f * MathF.Exp(-progress * 6f);
            phase += 2f * MathF.PI * freq / sr;
            float env = 1f - progress;
            buf[i] = MathF.Sin(phase) * env * 0.8f;
        }
        return buf;
    }

    private static float[] GenSiren(int sr)
    {
        float durSec = 1.0f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            // LFO modulates frequency between 500Hz and 1500Hz at 4Hz
            float lfo = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * 4f * t);
            float freq = 500f + 1000f * lfo;
            phase += 2f * MathF.PI * freq / sr;
            float env = FadeInOut(i, n, sr * 0.02f);
            buf[i] = MathF.Sin(phase) * env * 0.7f;
        }
        return buf;
    }

    private static float[] GenAlarm(int sr)
    {
        float durSec = 1.6f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            // Alternate between 800Hz and 1200Hz every 200ms
            int segment = (int)(t / 0.2f);
            float freq = segment % 2 == 0 ? 800f : 1200f;
            phase += 2f * MathF.PI * freq / sr;
            float env = FadeInOut(i, n, sr * 0.01f);
            // Square-ish wave for urgency
            float s = MathF.Sin(phase);
            buf[i] = (s >= 0 ? 0.7f : -0.7f) * env * 0.6f;
        }
        return buf;
    }

    private static float[] GenPowerUp(int sr)
    {
        // Three ascending notes: C5 (523Hz), E5 (659Hz), G5 (784Hz)
        float noteDur = 0.15f;
        float gap = 0.05f;
        float totalDur = noteDur * 3 + gap * 2 + 0.3f; // + tail
        int n = (int)(sr * totalDur);
        var buf = new float[n];
        float[] freqs = [523.25f, 659.25f, 783.99f];
        for (int note = 0; note < 3; note++)
        {
            float startSec = note * (noteDur + gap);
            int startSample = (int)(sr * startSec);
            int noteSamples = (int)(sr * noteDur);
            float phase = 0f;
            for (int j = 0; j < noteSamples && startSample + j < n; j++)
            {
                float t = (float)j / sr;
                float env = FadeInOut(j, noteSamples, sr * 0.01f);
                phase += 2f * MathF.PI * freqs[note] / sr;
                buf[startSample + j] += MathF.Sin(phase) * env * 0.5f;
            }
        }
        // Simple reverb tail: repeat each note quieter and delayed
        int tailDelay = (int)(sr * 0.08f);
        for (int i = tailDelay; i < n; i++)
        {
            buf[i] += buf[i - tailDelay] * 0.25f;
        }
        // Normalize
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    // ── MEMES ────────────────────────────────────────────────────────────

    private static float[] GenWhoosh(int sr)
    {
        float durSec = 0.7f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(21);
        // Band-shaped noise with swell envelope
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            float env = MathF.Sin(MathF.PI * progress); // smooth swell
            env *= env;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            // one-pole LP whose cutoff rises then falls
            float cutoff = 0.08f + 0.35f * MathF.Sin(MathF.PI * progress);
            lp += cutoff * (white - lp);
            buf[i] = lp * env * 1.6f;
        }
        return buf;
    }

    private static float[] GenExplosion(int sr)
    {
        float durSec = 1.6f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(66);
        float lp = 0f;
        // Crackle: random decaying impulse bursts + low rumble
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float env = ExpDecay(tMs, 500f, durSec * 1000f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.06f * (white - lp); // low rumble
            float crackle = white * (rng.NextDouble() > 0.97 ? 1f : 0.15f);
            buf[i] = (lp * 2.2f + crackle * 0.35f) * env * 0.75f;
        }
        return buf;
    }

    private static float[] GenHeartbeat(int sr)
    {
        float durSec = 1.2f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        // lub-dub: two low thumps 180ms apart
        float[] beats = [0f, 0.18f];
        foreach (var b in beats)
        {
            int start = (int)(sr * b);
            float phase = 0f;
            for (int j = 0; j < (int)(sr * 0.15f) && start + j < n; j++)
            {
                float tMs = j * 1000f / sr;
                float freq = 55f + 25f * MathF.Exp(-tMs / 30f);
                phase += 2f * MathF.PI * freq / sr;
                float env = ExpDecay(tMs, 55f, 150f);
                buf[start + j] += MathF.Sin(phase) * env * 0.85f;
            }
        }
        return buf;
    }

    private static float[] GenKnock(int sr)
    {
        float durSec = 0.9f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(11);
        // Three knocks at 0 / 0.28 / 0.56 s
        float[] hits = [0f, 0.28f, 0.56f];
        foreach (var h in hits)
        {
            int start = (int)(sr * h);
            for (int j = 0; j < (int)(sr * 0.08f) && start + j < n; j++)
            {
                float tMs = j * 1000f / sr;
                float env = ExpDecay(tMs, 18f, 80f);
                float thump = MathF.Sin(2f * MathF.PI * 160f * tMs / 1000f) * env;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.4f;
                buf[start + j] += (thump + noise) * 0.7f;
            }
        }
        return buf;
    }

    private static float[] GenGlassBreak(int sr)
    {
        float durSec = 0.9f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        var rng = new Random(54);
        // Initial smash + descending sparse high-freq jingle
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float env = ExpDecay(tMs, 250f, durSec * 1000f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            // random high resonators sparkle
            float sparkle = 0f;
            if (rng.NextDouble() > 0.92)
            {
                float f = 2500f + (float)rng.NextDouble() * 4500f;
                sparkle = MathF.Sin(2f * MathF.PI * f * t) * 0.5f;
            }
            buf[i] = (noise * 0.35f + sparkle) * env * 0.7f;
        }
        // Sharp attack
        for (int i = 0; i < (int)(sr * 0.02f); i++)
            buf[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - i / (sr * 0.02f)) * 0.9f;
        return buf;
    }

    private static float[] GenTeleport(int sr)
    {
        float durSec = 0.6f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        // Rising chirp with vibrato + shimmer
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            float baseFreq = 400f + 2400f * progress * progress;
            float vib = 1f + 0.06f * MathF.Sin(2f * MathF.PI * 30f * t);
            phase += 2f * MathF.PI * baseFreq * vib / sr;
            float env = MathF.Sin(MathF.PI * progress);
            buf[i] = (MathF.Sin(phase) + 0.4f * MathF.Sin(phase * 2f)) * env * 0.6f;
        }
        return buf;
    }

    private static float[] GenBuzzer(int sr)
    {
        float durSec = 0.5f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        // Harsh low square with slight amplitude wobble
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float square = MathF.Sin(2f * MathF.PI * 110f * t) >= 0 ? 1f : -1f;
            float wob = 0.85f + 0.15f * MathF.Sin(2f * MathF.PI * 18f * t);
            float env = FadeInOut(i, n, sr * 0.01f);
            buf[i] = square * wob * env * 0.5f;
        }
        return buf;
    }

    // ── NEW: MEMES ───────────────────────────────────────────────────────
}
