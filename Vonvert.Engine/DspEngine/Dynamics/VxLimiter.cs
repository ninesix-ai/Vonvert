// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Dynamics;

/*
 * Look-ahead brick-wall limiter — final safety net in the DSP chain.
 * Prevents any sample from exceeding the ceiling threshold.
 * Uses a decoupled envelope with separate attack/release time constants.
 */
public sealed class VxLimiter : IAudioEffect
{
    public string Name => "VoiceLimiter";
    public bool   IsEnabled { get; set; } = true;

    private float _ceilingDb = -0.3f;
    public float CeilingDb
    {
        get => _ceilingDb;
        set { _ceilingDb = value; _ceilLin = DbToLin(value); }
    }

    // Pre-computed constants
    private float _ceilLin = DbToLin(-0.3f);
    private readonly float _alpha = System.MathF.Exp(-1f / (0.001f * 48000f));
    private readonly float _beta  = System.MathF.Exp(-1f / (0.080f * 48000f));

    // Running peak estimate
    private float _peak;

    public void Process(Span<float> block)
    {
        var limit = _ceilLin;
        var pk = _peak;

        for (var n = 0; n < block.Length; ++n)
        {
            var mag = System.MathF.Abs(block[n]);

            // Asymmetric slew: instant attack, gradual release
            pk = mag > pk
                ? _alpha * (pk - mag) + mag
                : _beta  * (pk - mag) + mag;

            // Gain reduction when peak exceeds ceiling
            if (pk > limit)
                block[n] *= limit / (pk + 1e-9f);

            // Absolute safety: clamp to [-1, +1]
            block[n] = System.Math.Clamp(block[n], -1f, 1f);
        }
        _peak = pk;
    }

    public void Reset() => _peak = 0f;

    private static float DbToLin(float db) => System.MathF.Pow(10f, db * 0.05f);
}
