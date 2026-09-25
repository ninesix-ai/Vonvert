// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Lo-fi reverb — a compact Schroeder network (4 parallel combs into 2 series
 * allpasses) whose wet tail is then sample-rate reduced and bit-quantised.
 * The degradation applies to the reflections only, so the direct voice stays
 * clean while the space around it crunches into "broken hardware".
 *
 * Parameters:
 *   - RoomSize:   comb delay-length multiplier (1x .. 3x) — how large the
 *                 space sounds. Slewed per sample so dragging it never clicks.
 *   - Decay:      comb feedback gain (0 .. 0.95) — how long the tail lasts.
 *   - Downsample: sample-and-hold factor on the wet signal (1 = bypass)
 *   - BitCrush:   quantisation depth in bits (16 = bypass)
 *   - Mix:        dry/wet blend (0 = unprocessed)
 *
 * The comb sum is scaled by (1 - Decay) so lengthening the tail does not also
 * raise the perceived loudness — a comb loop's steady-state gain is
 * 1/(1-feedback), which otherwise reaches 20x at Decay=0.95.
 *
 * All buffers are allocated once at construction; no heap allocation in Process().
 */
public sealed class LoFiReverbEffect : IAudioEffect
{
    public string Name      { get; } = "LoFiReverb";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _roomSize = 0.5f;
    /// <summary>Room size (0 – 1): scales the comb delay lengths. Default: 0.5.</summary>
    public float RoomSize
    {
        get => _roomSize;
        set { _roomSize = Math.Clamp(value, 0f, 1f); Recalculate(); }
    }

    private float _decay = 0.6f;
    /// <summary>Comb feedback / tail length (0 – 0.95). Default: 0.6.</summary>
    public float Decay
    {
        get => _decay;
        set => _decay = Math.Clamp(value, 0f, 0.95f);
    }

    private int _downsample = 4;
    /// <summary>Sample rate reduction factor (1 – 20). Default: 4.</summary>
    public int Downsample
    {
        get => _downsample;
        set => _downsample = Math.Clamp(value, 1, 20);
    }

    private float _bitCrush = 8f;
    /// <summary>Bit depth (1 – 16). Default: 8.</summary>
    public float BitCrush
    {
        get => _bitCrush;
        set => _bitCrush = Math.Clamp(value, 1f, 16f);
    }

    private float _mix = 0.35f;
    /// <summary>Wet/dry blend (0 – 1). Default: 0.35.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    // ── Reverb structure: 4 parallel combs → 2 series allpasses ──────────

    // Tuning taps in samples at the engine rate; RoomSize scales them, so the
    // capacity has to cover the widest room (960 * 3).
    private static readonly int[] CombBaseTaps = [600, 720, 840, 960];
    private const int CombCapacity = 4096;            // power of two ≥ 960 * 3
    private const int CombWrapMask = CombCapacity - 1;

    private readonly float[][] _combBuffers;
    private readonly int[]     _combWritePos;
    private readonly float[]   _combTapTarget;        // desired length per comb
    private readonly float[]   _combTapCurrent;       // slewed actual length

    // ~50 ms time constant: far slower than a sample block, so a slider drag
    // glides the tap length instead of stepping it (which would click).
    private static readonly float TapSlew = 1f - MathF.Exp(-1f / (0.05f * AudioConstants.EngineRate));

    // Allpass delays
    private static readonly int[] AllpassDelays = [225, 150];
    private readonly float[][] _allpassBuffers;
    private readonly int[] _allpassIndices;
    private const float ALLPASS_FB = 0.5f;

    // ── Lo-fi processing state ───────────────────────────────────────────

    private int _holdCounter;
    private float _heldSample;

    public LoFiReverbEffect()
    {
        _combBuffers    = CombBaseTaps.Select(_ => new float[CombCapacity]).ToArray();
        _combWritePos   = new int[CombBaseTaps.Length];
        _combTapTarget  = new float[CombBaseTaps.Length];
        _combTapCurrent = new float[CombBaseTaps.Length];
        _allpassBuffers = AllpassDelays.Select(n => new float[n]).ToArray();
        _allpassIndices = new int[AllpassDelays.Length];

        Recalculate();
        Array.Copy(_combTapTarget, _combTapCurrent, _combTapTarget.Length);
    }

    /// <summary>Recompute comb lengths from RoomSize (1x .. 3x the tuning taps).</summary>
    private void Recalculate()
    {
        float scale = 1f + _roomSize * 2f;
        for (int c = 0; c < CombBaseTaps.Length; c++)
            _combTapTarget[c] = CombBaseTaps[c] * scale;
    }

    public void Process(Span<float> buf)
    {
        float wet       = _mix;
        float dry       = 1f - wet;
        float feedback  = _decay;
        float combNorm  = 0.25f * (1f - _decay);   // 4 combs, loudness-compensated
        int   dsFactor  = _downsample;
        float levels    = MathF.Pow(2f, _bitCrush);
        float invLevels = 1f / levels;

        for (int i = 0; i < buf.Length; i++)
        {
            float input = buf[i];
            float reverbOut = 0f;

            // ── Parallel comb filter bank ────────────────────────────────
            for (int c = 0; c < CombBaseTaps.Length; c++)
            {
                int     pos  = _combWritePos[c];
                float[] ring = _combBuffers[c];

                // Slew toward the target length, then read fractionally so the
                // interpolated tail follows RoomSize smoothly.
                _combTapCurrent[c] += (_combTapTarget[c] - _combTapCurrent[c]) * TapSlew;
                float rd = _combTapCurrent[c];
                int   d0 = (int)rd;
                float fr = rd - d0;
                int r0 = (pos - d0     + CombCapacity) & CombWrapMask;
                int r1 = (pos - d0 - 1 + CombCapacity) & CombWrapMask;
                float delayed = ring[r0] + fr * (ring[r1] - ring[r0]);

                ring[pos] = input + delayed * feedback;
                _combWritePos[c] = (pos + 1) & CombWrapMask;
                reverbOut += delayed;
            }
            reverbOut *= combNorm;

            // ── Series allpass filter bank ───────────────────────────────
            for (int a = 0; a < AllpassDelays.Length; a++)
            {
                int idx = _allpassIndices[a];
                float delayed = _allpassBuffers[a][idx];
                _allpassBuffers[a][idx] = reverbOut + delayed * ALLPASS_FB;
                _allpassIndices[a] = (idx + 1) % _allpassBuffers[a].Length;
                reverbOut = delayed - reverbOut;
            }

            // ── Lo-fi degradation on wet signal ─────────────────────────

            // Sample-rate reduction (sample-and-hold)
            _holdCounter++;
            if (_holdCounter >= dsFactor)
            {
                _holdCounter = 0;
                _heldSample = reverbOut;
            }
            float degraded = _heldSample;

            // Bit-depth reduction (quantization)
            degraded = MathF.Round(degraded * levels) * invLevels;

            // ── Output mix ──────────────────────────────────────────────
            buf[i] = input * dry + degraded * wet;
        }
    }

    public void Reset()
    {
        foreach (var b in _combBuffers) Array.Clear(b);
        foreach (var b in _allpassBuffers) Array.Clear(b);
        Array.Clear(_allpassIndices);
        Array.Clear(_combWritePos);
        Array.Copy(_combTapTarget, _combTapCurrent, _combTapTarget.Length);
        _holdCounter = 0;
        _heldSample = 0f;
    }
}
