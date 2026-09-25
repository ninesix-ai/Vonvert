// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Memes sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenAirHorn(int sr)
    {
        float durSec = 1.0f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            // 2500Hz sawtooth with 8Hz tremolo
            float tremolo = 0.6f + 0.4f * MathF.Sin(2f * MathF.PI * 8f * t);
            float sawPhase = (2500f * t) % 1f;
            float saw = 2f * sawPhase - 1f;
            float env = FadeInOut(i, n, sr * 0.02f);
            buf[i] = saw * tremolo * env * 0.7f;
        }
        return buf;
    }

    private static float[] GenSadTrombone(int sr)
    {
        float durSec = 1.5f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            // Glissando from 300Hz down to 150Hz
            float freq = 300f - 150f * progress;
            phase += 2f * MathF.PI * freq / sr;
            // Sawtooth for brass-like timbre
            float saw = 2f * ((phase / (2f * MathF.PI)) % 1f) - 1f;
            float env = FadeInOut(i, n, sr * 0.05f);
            buf[i] = saw * env * 0.6f;
        }
        return buf;
    }

    private static float[] GenTaDa(int sr)
    {
        // D4 (293.66Hz) for 0.2s, then A4 (440Hz) for 0.6s
        float dur1 = 0.2f, dur2 = 0.6f, gap = 0.05f;
        float total = dur1 + gap + dur2;
        int n = (int)(sr * total);
        var buf = new float[n];
        WriteNote(buf, sr, 0, dur1, 293.66f);
        WriteNote(buf, sr, (int)(sr * (dur1 + gap)), dur2, 440f);
        // Normalize
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    // ── MUSIC ────────────────────────────────────────────────────────────

    private static float[] GenDunDunDun(int sr)
    {
        // Three descending minor stabs: A3, F3, D3
        float[] freqs = [220f, 174.61f, 146.83f];
        float noteDur = 0.28f;
        float gap = 0.12f;
        float total = noteDur * 3 + gap * 2 + 0.5f;
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int note = 0; note < 3; note++)
        {
            int start = (int)(sr * note * (noteDur + gap));
            float phase = 0f;
            for (int j = 0; j < (int)(sr * noteDur) && start + j < n; j++)
            {
                float tMs = j * 1000f / sr;
                float env = ExpDecay(tMs, 180f, noteDur * 1000f);
                phase += 2f * MathF.PI * freqs[note] / sr;
                float s = MathF.Sin(phase) + 0.5f * MathF.Sin(2f * phase);
                buf[start + j] += s * env * 0.45f;
            }
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenBoing(int sr)
    {
        float durSec = 0.8f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        // Spring: descending pitch with vibrato that slows down
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float progress = t / durSec;
            float freq = 600f - 350f * progress;
            float vibRate = 22f * (1f - progress * 0.8f);
            float vib = 1f + 0.5f * MathF.Exp(-progress * 2.5f) * MathF.Sin(2f * MathF.PI * vibRate * t);
            phase += 2f * MathF.PI * freq * vib / sr;
            float env = ExpDecay(tMs, 400f, durSec * 1000f);
            buf[i] = MathF.Sin(phase) * env * 0.7f;
        }
        return buf;
    }

    // ── NEW: MUSIC ───────────────────────────────────────────────────────
}
