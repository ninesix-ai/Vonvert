// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Music sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenCMajorScale(int sr)
    {
        // C4-D4-E4-F4-G4-A4-B4-C5 as eighth notes at 120 BPM
        float[] freqs = [261.63f, 293.66f, 329.63f, 349.23f, 392.00f, 440.00f, 493.88f, 523.25f];
        float noteDur = 0.25f; // eighth note at 120 BPM
        float total = noteDur * freqs.Length + 0.2f; // + tail
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int note = 0; note < freqs.Length; note++)
        {
            int start = (int)(sr * note * noteDur);
            WriteNote(buf, sr, start, noteDur, freqs[note]);
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenChord(int sr, float[] freqs, float durSec)
    {
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float[] phases = new float[freqs.Length];
        for (int i = 0; i < n; i++)
        {
            float env = FadeInOut(i, n, sr * 0.05f);
            float s = 0f;
            for (int f = 0; f < freqs.Length; f++)
            {
                phases[f] += 2f * MathF.PI * freqs[f] / sr;
                s += MathF.Sin(phases[f]);
            }
            buf[i] = s / freqs.Length * env * 0.7f;
        }
        return buf;
    }

    // ── AMBIENT (NOISE) ──────────────────────────────────────────────────

    private static float[] GenArpeggio(int sr)
    {
        // Am arpeggio loop: A3-C4-E4-A4-E4-C4 (two passes)
        float[] freqs = [220f, 261.63f, 329.63f, 440f, 329.63f, 261.63f];
        float noteDur = 0.14f;
        int passes = 2;
        float total = noteDur * freqs.Length * passes + 0.15f;
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int pass = 0; pass < passes; pass++)
        {
            for (int note = 0; note < freqs.Length; note++)
            {
                int start = (int)(sr * (pass * freqs.Length + note) * noteDur);
                float phase = 0f;
                for (int j = 0; j < (int)(sr * noteDur * 1.1f) && start + j < n; j++)
                {
                    float env = FadeInOut(j, (int)(sr * noteDur * 1.1f), sr * 0.008f);
                    phase += 2f * MathF.PI * freqs[note] / sr;
                    buf[start + j] += MathF.Sin(phase) * env * 0.4f;
                }
            }
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenBassLine(int sr)
    {
        // Simple funk bass: E1-E1-G1-A1 pattern
        float[] freqs = [41.2f, 41.2f, 49f, 55f];
        float noteDur = 0.22f;
        float total = noteDur * freqs.Length * 2;
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int rep = 0; rep < 2; rep++)
        {
            for (int note = 0; note < freqs.Length; note++)
            {
                int start = (int)(sr * (rep * freqs.Length + note) * noteDur);
                float phase = 0f;
                for (int j = 0; j < (int)(sr * noteDur * 0.9f) && start + j < n; j++)
                {
                    float tMs = j * 1000f / sr;
                    float env = ExpDecay(tMs, 120f, noteDur * 900f);
                    phase += 2f * MathF.PI * freqs[note] / sr;
                    // saturated sine for punch
                    float s = MathF.Tanh(2.2f * MathF.Sin(phase));
                    buf[start + j] += s * env * 0.6f;
                }
            }
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenBell(int sr)
    {
        float durSec = 2.0f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        // FM bell: carrier 660Hz modulated by 440Hz index decaying
        float carPhase = 0f, modPhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float t = (float)i / sr;
            float env = ExpDecay(tMs, 700f, durSec * 1000f);
            float index = 3f * MathF.Exp(-t * 2.5f);
            modPhase += 2f * MathF.PI * 440f / sr;
            float mod = MathF.Sin(modPhase) * index;
            carPhase += 2f * MathF.PI * (660f + 440f * mod) / sr;
            buf[i] = MathF.Sin(carPhase) * env * 0.55f;
        }
        return buf;
    }

    // ── NEW: RETRO (8-BIT) ───────────────────────────────────────────────
}
