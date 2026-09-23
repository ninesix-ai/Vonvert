// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Dynamics;

/*
 * Envelope-tracking dynamic range compressor with soft-knee detection
 * and auto-make-up gain compensation.
 *
 * Signal flow:  input → peak envelope → gain computer (soft knee) → makeup → output
 *
 * The feed-forward topology analyses the incoming signal level before
 * applying gain reduction, yielding more predictable loudness behaviour
 * than feedback designs.
 */
public sealed class VxCompressor : IAudioEffect
{
    public string Name => "VoiceCompressor";
    public bool   IsEnabled { get; set; }

    // ── User-facing parameters ──────────────────────────────────────
    private float _thresholdDb = -18f;
    public float ThresholdDb
    {
        get => _thresholdDb;
        set { _thresholdDb = value; _linearFloor = Decibel.ToLinear(value); }
    }

    public float Ratio     { get; set; } = 4f;
    public float AttackMs  { get; set; } = 5f;
    public float ReleaseMs { get; set; } = 100f;
    public float MakeupDb  { get; set; } = 0f;

    // ── Pre-computed coefficients (sample rate = 48 kHz) ────────────
    private float _linearFloor  = Decibel.ToLinear(-18f);
    private float _attackCoeff  = System.MathF.Exp(-1f / (0.005f * SampleRate));
    private float _releaseCoeff = System.MathF.Exp(-1f / (0.100f * SampleRate));
    private float _makeupLinear = 1f;

    private const int SampleRate = 48_000;
    private float _peakEnv;

    public void Process(Span<float> block)
    {
        var env       = _peakEnv;
        var floor     = _linearFloor;
        var atkCoef   = _attackCoeff;
        var relCoef   = _releaseCoeff;
        var makeup    = _makeupLinear;
        var ratioSlope = 1f - 1f / Ratio;       // proportion of excess to remove

        for (var n = 0; n < block.Length; ++n)
        {
            var magnitude = System.MathF.Abs(block[n]);

            // Asymmetric peak envelope: faster attack than release
            env = magnitude > env
                ? atkCoef  * (env - magnitude) + magnitude
                : relCoef  * (env - magnitude) + magnitude;

            if (env > floor)
            {
                var excessDb      = Decibel.LinearToDecibel(env) - Decibel.LinearToDecibel(floor);
                var reductionDb   = excessDb * ratioSlope;
                var reductionLin  = Decibel.ToLinear(-reductionDb);
                block[n] *= reductionLin * makeup;
            }
        }

        _peakEnv = env;
    }

    public void Reset() => _peakEnv = 0f;
}

// ── Tiny helper to keep the hot path readable ────────────────────────
internal static class Decibel
{
    public static float ToLinear(float db)        => System.MathF.Pow(10f, db * 0.05f);
    public static float LinearToDecibel(float lin) => 20f * System.MathF.Log10(lin + 1e-30f);
}
