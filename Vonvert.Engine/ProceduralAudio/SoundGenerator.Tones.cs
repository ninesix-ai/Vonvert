// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// SoundGenerator — Tones sound generation methods.
/// </summary>
public static partial class SoundGenerator
{

    private static float[] GenBassDrop(int sr)
    {
        float durSec = 0.8f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float tMs = t * 1000f;
            // Frequency slides from 200Hz down to 30Hz
            float freq = 30f + 170f * MathF.Exp(-tMs / 300f);
            phase += 2f * MathF.PI * freq / sr;
            float env = ExpDecay(tMs, 300f, durSec * 1000f);
            buf[i] = MathF.Sin(phase) * env * 0.8f;
        }
        return buf;
    }

    // ── SFX ──────────────────────────────────────────────────────────────

    private static float[] GenSubBoom(int sr)
    {
        float durSec = 1.2f;
        int n = (int)(sr * durSec);
        var buf = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float tMs = i * 1000f / sr;
            float freq = 45f + 25f * MathF.Exp(-tMs / 400f);
            phase += 2f * MathF.PI * freq / sr;
            float env = ExpDecay(tMs, 450f, durSec * 1000f);
            buf[i] = MathF.Sin(phase) * env * 0.9f;
        }
        return buf;
    }

    // ── NEW: SFX ─────────────────────────────────────────────────────────
}
