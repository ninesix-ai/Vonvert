// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Retro sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenCoin(int sr)
    {
        // Classic coin: B5 square, quick slide to E6
        float durSec = 0.45f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float freq = t < 0.08f ? 987.77f : 1318.51f;
            phase += 2f * MathF.PI * freq / sr;
            float square = MathF.Sin(phase) >= 0 ? 1f : -1f;
            float env = t < 0.08f ? 1f : ExpDecay((t - 0.08f) * 1000f, 130f, (durSec - 0.08f) * 1000f);
            buf[i] = square * env * 0.35f;
        }
        return buf;
    }

    private static float[] GenJump(int sr)
    {
        // Fast rising square blip
        float durSec = 0.22f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float progress = t / durSec;
            float freq = 180f + 520f * progress;
            phase += 2f * MathF.PI * freq / sr;
            float square = MathF.Sin(phase) >= 0 ? 1f : -1f;
            float env = 1f - progress * 0.6f;
            buf[i] = square * env * 0.3f;
        }
        return buf;
    }

    private static float[] GenBlip(int sr)
    {
        // UI select blip: short 1200Hz square
        float durSec = 0.08f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float square = MathF.Sin(2f * MathF.PI * 1200f * t) >= 0 ? 1f : -1f;
            float env = FadeInOut(i, n, sr * 0.005f);
            buf[i] = square * env * 0.28f;
        }
        return buf;
    }

    private static float[] GenGameOver(int sr)
    {
        // Descending 3-note: C4, G3, E3 with final low thud
        float[] freqs = [261.63f, 196f, 164.81f];
        float noteDur = 0.3f;
        float gap = 0.1f;
        float total = noteDur * 3 + gap * 2 + 0.4f;
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int note = 0; note < 3; note++)
        {
            int start = (int)(sr * note * (noteDur + gap));
            float phase = 0f;
            for (int j = 0; j < (int)(sr * noteDur) && start + j < n; j++)
            {
                float env = FadeInOut(j, (int)(sr * noteDur), sr * 0.01f);
                phase += 2f * MathF.PI * freqs[note] / sr;
                float square = MathF.Sin(phase) >= 0 ? 1f : -1f;
                buf[start + j] += square * env * 0.28f;
            }
        }
        // final thud
        int thudStart = (int)(sr * (3 * (noteDur + gap)));
        float thudPhase = 0f;
        for (int j = 0; thudStart + j < n; j++)
        {
            float tMs = j * 1000f / sr;
            float freq = 80f + 40f * MathF.Exp(-tMs / 60f);
            thudPhase += 2f * MathF.PI * freq / sr;
            float env = ExpDecay(tMs, 120f, (total - 3 * (noteDur + gap)) * 1000f);
            buf[thudStart + j] += MathF.Sin(thudPhase) * env * 0.5f;
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    private static float[] GenLevelUp(int sr)
    {
        // Fast major arpeggio up: C5-E5-G5-C6
        float[] freqs = [523.25f, 659.25f, 783.99f, 1046.5f];
        float noteDur = 0.09f;
        float total = noteDur * freqs.Length + 0.25f;
        int n = (int)(sr * total);
        var buf = new float[n];
        for (int note = 0; note < freqs.Length; note++)
        {
            int start = (int)(sr * note * noteDur);
            float phase = 0f;
            for (int j = 0; j < (int)(sr * noteDur * 1.4f) && start + j < n; j++)
            {
                float env = FadeInOut(j, (int)(sr * noteDur * 1.4f), sr * 0.006f);
                phase += 2f * MathF.PI * freqs[note] / sr;
                float square = MathF.Sin(phase) >= 0 ? 1f : -1f;
                buf[start + j] += square * env * 0.26f;
            }
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = MathF.Max(peak, MathF.Abs(buf[i]));
        if (peak > 0f) for (int i = 0; i < n; i++) buf[i] /= peak;
        return buf;
    }

    // ── NEW: AMBIENT (NATURE) ────────────────────────────────────────────
}
