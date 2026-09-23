// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Asymmetric soft-clip saturation (distortion / drive).
 * Positive peaks use tanh, negative peaks use a slightly harder cubic curve —
 * this even-harmonic asymmetry sounds warmer than symmetric clipping.
 * Drive 0–1 controls pre-gain (1× … 21×); the output is normalised by the
 * tanh ceiling so the curve approaches but never exceeds ±1.
 *
 * Stateless: Process() allocates nothing and Reset() has nothing to clear.
 */
public sealed class VxDrive : IAudioEffect
{
    public string Name => "VoiceDrive";
    public bool   IsEnabled { get; set; }

    private float _amount = 0.5f;
    public float Drive
    {
        get => _amount;
        set => _amount = System.Math.Clamp(value, 0f, 1f);
    }

    public void Process(Span<float> block)
    {
        var pregain = 1f + _amount * 20f;
        var ceil    = 1f / System.MathF.Tanh(pregain);

        for (var i = 0; i < block.Length; ++i)
        {
            var x = block[i] * pregain;
            // positive half: tanh; negative half: cubic soft-clip
            block[i] = x >= 0f
                ?  System.MathF.Tanh(x) * ceil
                :  (x / (1f + x * x)) * ceil;
        }
    }

    public void Reset() { /* stateless */ }
}
