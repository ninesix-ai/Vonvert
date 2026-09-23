// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Schroeder-Moorer reverb network (Freeverb variant).
 *
 * Architecture:
 *   • 8 parallel comb filters with interleaved damping — model early
 *     reflections and room resonance modes.
 *   • 4 series allpass filters — diffuse the reflections into a dense
 *     late-reverb tail.
 *
 * Buffers use power-of-2 sizes with bitmask wrapping for branch-free
 * index arithmetic on the real-time DSP path.
 */
public sealed class VxReverb : IAudioEffect
{
    public string Name      { get; } = "VoiceReverb";
    public bool   IsEnabled { get; set; }

    // ── Parameters ──────────────────────────────────────────────────
    private float _wet      = 0.3f;   // wet send level
    private float _roomSize = 0.5f;   // room size 0–1
    private float _dry      = 0.7f;   // dry passthrough
    private float _damping  = 0.5f;   // high-freq absorption

    public float RoomSize { get => _roomSize; set { _roomSize = Math.Clamp(value, 0f, 1f); Recalculate(); } }
    public float Damping  { get => _damping;  set { _damping  = Math.Clamp(value, 0f, 1f); Recalculate(); } }
    public float Wet { get => _wet; set { _wet = Math.Clamp(value, 0f, 1f); _dry = 1f - _wet; } }
    public float Dry => 1f - _wet;

    // ── Comb filter bank (8 parallel, interleaved damping) ──────────
    // Delay lengths from the Freeverb standard tuning.
    private static readonly int[] CombTaps = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
    private const int CombCapacity = 2048;   // next power-of-2 ≥ max(CombTaps)
    private const int CombWrapMask = CombCapacity - 1;

    private readonly float[][] _combRing;       // delay-line buffers [8][]
    private readonly int[]     _combWritePos;   // current write index per comb
    private readonly float[]   _combDampState;  // interleaved damping LPF state
    private float _combFeedback;                // room-size-dependent feedback
    private float _dampCoeffFast, _dampCoeffSlow;

    // ── Allpass filter bank (4 series) ──────────────────────────────
    private static readonly int[] AllpassTaps = [556, 441, 341, 225];
    private const int ApCapacity = 1024;       // next power-of-2 ≥ max(AllpassTaps)
    private const int ApWrapMask = ApCapacity - 1;

    private readonly float[][] _apRing;         // delay-line buffers [4][]
    private readonly int[]     _apWritePos;     // current write index per allpass
    private const float ApReflect = 0.5f;      // allpass reflection coefficient

    // ── Constructor ─────────────────────────────────────────────────
    public VxReverb()
    {
        _combRing      = CombTaps.Select(_ => new float[CombCapacity]).ToArray();
        _combWritePos  = new int[8];
        _combDampState = new float[8];

        _apRing     = AllpassTaps.Select(_ => new float[ApCapacity]).ToArray();
        _apWritePos = new int[4];

        Recalculate();
    }

    // Recompute damping and feedback from current RoomSize / Damping
    private void Recalculate()
    {
        _combFeedback  = 0.28f + _roomSize * 0.7f;
        _dampCoeffFast = _damping * 0.4f;
        _dampCoeffSlow = 1f - _dampCoeffFast;
    }

    // ── DSP entry point ─────────────────────────────────────────────
    public void Process(Span<float> buf)
    {
        for (var i = 0; i < buf.Length; i++)
        {
            var dryInput = buf[i];
            var excitation = dryInput + 1e-25f;   // anti-denormal bias

            var combSum = RunCombBank(excitation);
            var tail    = RunAllpassBank(combSum);

            buf[i] = dryInput * _dry + tail * _wet * 0.015f;
        }
    }

    // Parallel comb filter bank — models room modes and early reflections
    private float RunCombBank(float excitation)
    {
        float accumulator = 0f;
        for (var c = 0; c < 8; c++)
        {
            var pos  = _combWritePos[c];
            var ring = _combRing[c];
            // Read the sample written CombTaps[c] steps ago — the per-comb tuned
            // delay length. Reading ring[pos] directly would make every comb delay
            // equal the buffer capacity (2048) instead of its tap, collapsing the
            // Freeverb decorrelation into a single metallic resonance.
            var readIdx = (pos - CombTaps[c] + CombCapacity) & CombWrapMask;
            var stored  = ring[readIdx];

            // Interleaved one-pole damping filter
            _combDampState[c] = stored * _dampCoeffSlow + _combDampState[c] * _dampCoeffFast;
            ring[pos] = excitation + _combDampState[c] * _combFeedback;

            _combWritePos[c] = (pos + 1) & CombWrapMask;
            accumulator += stored;
        }
        return accumulator;
    }

    // Series allpass bank — diffuses comb output into dense late reverb
    private float RunAllpassBank(float input)
    {
        var signal = input;
        for (var a = 0; a < 4; a++)
        {
            var pos  = _apWritePos[a];
            var ring = _apRing[a];
            // Delay by AllpassTaps[a] samples, not the full buffer capacity (1024).
            var readIdx = (pos - AllpassTaps[a] + ApCapacity) & ApWrapMask;
            var buf0 = ring[readIdx];

            ring[pos] = signal + buf0 * ApReflect;
            _apWritePos[a] = (pos + 1) & ApWrapMask;

            signal = buf0 - signal;
        }
        return signal;
    }

    public void Reset()
    {
        foreach (var b in _combRing)  System.Array.Clear(b);
        foreach (var b in _apRing)    System.Array.Clear(b);
        System.Array.Clear(_combDampState);
        System.Array.Clear(_combWritePos);
        System.Array.Clear(_apWritePos);
    }
}
