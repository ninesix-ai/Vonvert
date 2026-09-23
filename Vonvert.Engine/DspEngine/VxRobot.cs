// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Robot / talkbox timbre via ring modulation with a sine carrier.
 * The carrier frequency is adjustable (20–200 Hz); a pre-built sine LUT
 * keeps the per-sample cost to a table lookup and an integer add.
 *
 * Real-time contract: Process() allocates nothing and touches no locks —
 * the static LUT is built once at type init.
 */
public sealed class VxRobot : IAudioEffect
{
    public string Name => "VoiceRobot";
    public bool   IsEnabled { get; set; }

    private const int SR   = 48_000;
    private const int LUT  = 4096;
    private const int MASK = LUT - 1;

    private static readonly float[] _sinLut = BuildLut();

    private float _freqHz = 60f;
    public float CarrierFreq
    {
        get => _freqHz;
        set { _freqHz = System.Math.Clamp(value, 20f, 200f); RecomputeStep(); }
    }

    private int _phase;   // fixed-point phase accumulator (0 … LUT-1)
    private int _step;    // phase increment per sample

    public VxRobot() { RecomputeStep(); }

    public void Process(Span<float> block)
    {
        var ph = _phase;
        for (var i = 0; i < block.Length; ++i)
        {
            block[i] *= _sinLut[ph & MASK];
            ph += _step;
        }
        _phase = ph & MASK;
    }

    public void Reset() => _phase = 0;

    // Recompute the phase increment from the current carrier frequency.
    private void RecomputeStep()
        => _step = (int)(_freqHz / SR * LUT + 0.5f);

    private static float[] BuildLut()
    {
        var t = new float[LUT];
        for (var k = 0; k < LUT; k++)
            t[k] = System.MathF.Sin(System.MathF.Tau * k / LUT);
        return t;
    }
}
