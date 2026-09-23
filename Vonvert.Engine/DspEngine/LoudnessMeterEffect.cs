// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.Engine.DspEngine;

/*
 * Loudness Meter — ITU-R BS.1770-4 compliant loudness measurement.
 *
 * This is a pass-through effect: it does NOT modify the audio signal.
 * Instead, it computes loudness metrics in real-time:
 *
 *   - Momentary loudness:  400 ms sliding window (updated every 100 ms)
 *   - Short-term loudness: 3 s sliding window (updated every 100 ms)
 *   - Integrated loudness: gated average since Reset() (LUFS)
 *
 * Algorithm (ITU-R BS.1770-4):
 *   1. Apply K-weighting filter (stage 1: high-shelf, stage 2: highpass)
 *   2. Square the filtered signal
 *   3. Mean-square over the measurement window
 *   4. Convert to LUFS: -0.691 + 10·log10(mean_square)
 *
 * Use cases:
 *   - Real-time loudness display in the UI
 *   - Broadcast compliance monitoring (EBU R128: -23 LUFS target)
 *   - Normalisation target for batch processing
 *
 * Read CurrentMomentary / CurrentShortTerm / IntegratedLoudness from UI.
 * Zero-alloc on DSP thread.
 */
public sealed class LoudnessMeterEffect : IAudioEffect
{
    public string Name      { get; } = "LoudnessMeter";
    public bool   IsEnabled { get; set; } = true;

    // ── Read-only meter outputs ───────────────────────────────────────────

    /// <summary>Momentary loudness in LUFS (400 ms window). Updated ~10×/sec.</summary>
    public float CurrentMomentary { get; private set; } = -70f;

    /// <summary>Short-term loudness in LUFS (3 s window). Updated ~10×/sec.</summary>
    public float CurrentShortTerm { get; private set; } = -70f;

    /// <summary>Integrated (gated) loudness in LUFS since last Reset().</summary>
    public float IntegratedLoudness { get; private set; } = -70f;

    // ── K-weighting filter state ──────────────────────────────────────────

    private const float FS = 48000f;

    // Stage 1: High-shelf (approximate K-weighting pre-filter)
    // Coefficients for 48 kHz from ITU-R BS.1770-4
    private float _s1X1, _s1X2, _s1Y1, _s1Y2;
    private const float S1_B0 =  1.5308477f;
    private const float S1_B1 = -2.6509954f;
    private const float S1_B2 =  1.1622221f;
    private const float S1_A1 = -1.6906593f;
    private const float S1_A2 =  0.7333217f;

    // Stage 2: Highpass at 38 Hz (second-order)
    private float _s2X1, _s2X2, _s2Y1, _s2Y2;
    private const float S2_B0 =  1.0f;
    private const float S2_B1 = -2.0f;
    private const float S2_B2 =  1.0f;
    private const float S2_A1 = -1.9900474f;
    private const float S2_A2 =  0.9900723f;

    // ── Measurement buffers ───────────────────────────────────────────────

    private const int MOMENTARY_SAMPLES  = 19200;   // 400 ms @ 48 kHz
    private const int SHORTTERM_SAMPLES  = 144000;  // 3 s @ 48 kHz
    private const int BLOCK_SIZE         = 4800;    // 100 ms blocks

    private readonly float[] _blockBuffer = new float[BLOCK_SIZE];
    private int _blockPos = 0;

    // Circular buffer of block energies for sliding windows
    private readonly float[] _blockEnergies = new float[SHORTTERM_SAMPLES / BLOCK_SIZE]; // 30 blocks
    private int _blockIndex = 0;
    private int _blockCount = 0;

    // Integrated loudness accumulation
    private double _integratedSum  = 0;
    private int    _integratedBlocks = 0;

    // Gating threshold for integrated: -70 LUFS absolute gate
    private const float ABSOLUTE_GATE = -70f;

    public void Process(Span<float> buf)
    {
        // Pass-through: meter does not modify audio
        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];

            // K-weighting stage 1: high-shelf
            float s1 = S1_B0 * x + S1_B1 * _s1X1 + S1_B2 * _s1X2
                        - S1_A1 * _s1Y1 - S1_A2 * _s1Y2;
            _s1X2 = _s1X1; _s1X1 = x;
            _s1Y2 = _s1Y1; _s1Y1 = s1;

            // K-weighting stage 2: highpass
            float s2 = S2_B0 * s1 + S2_B1 * _s2X1 + S2_B2 * _s2X2
                        - S2_A1 * _s2Y1 - S2_A2 * _s2Y2;
            _s2X2 = _s2X1; _s2X1 = s1;
            _s2Y2 = _s2Y1; _s2Y1 = s2;

            // Accumulate squared value into current block
            _blockBuffer[_blockPos] = s2 * s2;
            _blockPos++;

            // When block is full, compute block energy and update meters
            if (_blockPos >= BLOCK_SIZE)
            {
                FlushBlock();
            }
        }
    }

    private void FlushBlock()
    {
        // Mean square of current block
        float sumSq = 0f;
        for (int i = 0; i < BLOCK_SIZE; i++)
            sumSq += _blockBuffer[i];
        float blockEnergy = sumSq / BLOCK_SIZE;

        // Store in circular block buffer
        _blockEnergies[_blockIndex] = blockEnergy;
        _blockIndex = (_blockIndex + 1) % _blockEnergies.Length;
        if (_blockCount < _blockEnergies.Length) _blockCount++;

        // Momentary: average of last 4 blocks (400 ms)
        int momBlocks = Math.Min(_blockCount, 4);
        float momEnergy = AverageBlocks(momBlocks);
        CurrentMomentary = EnergyToLufs(momEnergy);

        // Short-term: average of last 30 blocks (3 s)
        int stBlocks = Math.Min(_blockCount, 30);
        float stEnergy = AverageBlocks(stBlocks);
        CurrentShortTerm = EnergyToLufs(stEnergy);

        // Integrated: gated accumulation
        if (CurrentMomentary > ABSOLUTE_GATE)
        {
            _integratedSum += blockEnergy;
            _integratedBlocks++;
        }

        if (_integratedBlocks > 0)
            IntegratedLoudness = EnergyToLufs((float)(_integratedSum / _integratedBlocks));

        _blockPos = 0;
    }

    private float AverageBlocks(int count)
    {
        float sum = 0f;
        int start = (_blockIndex - count + _blockEnergies.Length) % _blockEnergies.Length;
        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % _blockEnergies.Length;
            sum += _blockEnergies[idx];
        }
        return sum / count;
    }

    private static float EnergyToLufs(float energy)
    {
        if (energy < 1e-20f) return -70f;
        return -0.691f + 10f * MathF.Log10(energy);
    }

    public void Reset()
    {
        _s1X1 = _s1X2 = _s1Y1 = _s1Y2 = 0f;
        _s2X1 = _s2X2 = _s2Y1 = _s2Y2 = 0f;
        _blockPos = 0;
        _blockIndex = 0;
        _blockCount = 0;
        Array.Clear(_blockEnergies);
        _integratedSum = 0;
        _integratedBlocks = 0;
        CurrentMomentary  = -70f;
        CurrentShortTerm  = -70f;
        IntegratedLoudness = -70f;
    }
}
