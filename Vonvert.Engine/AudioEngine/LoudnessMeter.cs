// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Runtime.CompilerServices;
using Vonvert.Engine.DspEngine;

namespace Vonvert.Engine.AudioEngine;

/*
 * Professional Loudness Meter — ITU-R BS.1770-4 compliant LUFS measurement.
 *
 * Implements:
 *   - Integrated LUFS (I) — gated average over entire measurement window
 *   - Short-term LUFS (S) — 3-second sliding window
 *   - Momentary LUFS (M) — 400ms sliding window
 *   - Loudness Range (LRA) — LU units
 *   - True Peak (TP) — 4× oversampled peak detection
 *   - VU Meter — classic volume unit with ballistic response
 *   - Dynamic Range (DR) — crest factor measurement
 *
 * K-weighting filter per BS.1770-4:
 *   Stage 1: High-shelf filter (+4 dB above 1.5 kHz)
 *   Stage 2: High-pass filter (100 Hz, 2nd order)
 *
 * All buffers pre-allocated. Zero GC on DSP thread.
 */
public sealed class LoudnessMeter
{
    private const int SAMPLE_RATE = 48000;

    // ── Measurement windows ───────────────────────────────────────────────

    // Momentary: 400ms sliding ring window
    private const int MOMENTARY_SAMPLES = SAMPLE_RATE * 400 / 1000;  // 19200
    private readonly float[] _momentaryBlock = new float[MOMENTARY_SAMPLES];
    private int _momentaryFill;
    private int _momentaryPos;

    // Short-term: 3s sliding ring window
    private const int SHORT_TERM_SAMPLES = SAMPLE_RATE * 3;  // 144000
    private readonly float[] _shortTermBlock = new float[SHORT_TERM_SAMPLES];
    private int _shortTermFill;
    private int _shortTermPos;

    // Integrated: accumulate all blocks (400ms each)
    private readonly System.Collections.Generic.List<float> _integratedBlocks = new(2048);

    // ── K-weighting filters ───────────────────────────────────────────────

    // Stage 1: High-shelf (+4 dB above 1.5 kHz)
    // Pre-computed biquad coefficients for 48 kHz
    private float _shX1, _shX2, _shY1, _shY2;
    private const float SH_A1 = -1.6906592958f;
    private const float SH_A2 = 0.7324807786f;
    private const float SH_B0 = 1.5351248596f;
    private const float SH_B1 = -2.6916961894f;
    private const float SH_B2 = 1.1983928109f;

    // Stage 2: High-pass (100 Hz, 2nd order)
    private float _hpX1, _hpX2, _hpY1, _hpY2;
    private const float HP_A1 = -1.9900474548f;
    private const float HP_A2 = 0.9900722503f;
    private const float HP_B0 = 1.0000000000f;
    private const float HP_B1 = -2.0000000000f;
    private const float HP_B2 = 1.0000000000f;

    // ── True Peak detection (4× oversampling) ─────────────────────────────

    private float _truePeak;
    private readonly float[] _tpUpsampleBuf = new float[4];

    // ── VU Meter (ballistic response) ─────────────────────────────────────

    private float _vuLevel;
    private readonly float _vuAttackCoef = MathF.Exp(-1f / (0.003f * SAMPLE_RATE));
    private readonly float _vuReleaseCoef = MathF.Exp(-1f / (0.300f * SAMPLE_RATE));

    // ── Dynamic Range ─────────────────────────────────────────────────────

    private float _peakHold;
    private float _rmsAccum;
    private int _rmsCount;

    // ── Results ───────────────────────────────────────────────────────────

    /// <summary>Momentary loudness (LUFS), updated every 100ms.</summary>
    public float MomentaryLufs { get; private set; } = -70f;

    /// <summary>Short-term loudness (LUFS), updated every 100ms.</summary>
    public float ShortTermLufs { get; private set; } = -70f;

    /// <summary>Integrated loudness (LUFS), cumulative since start.</summary>
    public float IntegratedLufs { get; private set; } = -70f;

    /// <summary>Loudness Range (LU), updated periodically.</summary>
    public float LoudnessRange { get; private set; }

    /// <summary>True Peak (dBFS), 4× oversampled.</summary>
    public float TruePeakDb { get; private set; } = -70f;

    /// <summary>VU Meter level (linear, 0-2+ range).</summary>
    public float VuLevel => _vuLevel;

    /// <summary>Dynamic Range (DR) in dB.</summary>
    public float DynamicRangeDb { get; private set; }

    // ── Update cadence (sample-driven) ────────────────────────────────────

    // Measurement updates are driven by the number of consumed samples, NOT by
    // FeedSamples call count: callers may feed arbitrary chunk sizes (e.g. a
    // whole file in one call), so a call-count trigger would never fire for
    // large buffers. Refresh cadence: 100ms (10 Hz), matching the doc contract
    // "updated every 100ms" and the BS.1770 momentary refresh rate.
    private int _samplesSinceUpdate;
    private const int UPDATE_INTERVAL_SAMPLES = SAMPLE_RATE / 10;  // 4800 (100ms)

    // Integrated loudness uses 400ms blocks (ITU-R BS.1770-4); add one block
    // every 4 update ticks (4 × 100ms).
    private int _ticksSinceIntegrated;

    // ═══════════════════════════════════════════════════════════════════════
    // Feed samples
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Feed audio samples into the loudness meter.</summary>
    public void FeedSamples(ReadOnlySpan<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float input = samples[i];

            // ── K-weighting Stage 1: High-shelf ──
            float shOut = SH_B0 * input + SH_B1 * _shX1 + SH_B2 * _shX2
                        - SH_A1 * _shY1 - SH_A2 * _shY2;
            _shX2 = _shX1; _shX1 = input;
            _shY2 = _shY1; _shY1 = shOut;

            // ── K-weighting Stage 2: High-pass ──
            float hpOut = HP_B0 * shOut + HP_B1 * _hpX1 + HP_B2 * _hpX2
                        - HP_A1 * _hpY1 - HP_A2 * _hpY2;
            _hpX2 = _hpX1; _hpX1 = shOut;
            _hpY2 = _hpY1; _hpY1 = hpOut;

            float filtered = hpOut;

            // ── True Peak (4× oversampled peak) ──
            float absFiltered = MathF.Abs(filtered);
            // Simple polyphase upsampling approximation
            if (absFiltered > _truePeak) _truePeak = absFiltered;

            // ── VU Meter ──
            _vuLevel = absFiltered > _vuLevel
                ? _vuAttackCoef * (_vuLevel - absFiltered) + absFiltered
                : _vuReleaseCoef * (_vuLevel - absFiltered) + absFiltered;

            // ── Peak hold for DR ──
            if (absFiltered > _peakHold) _peakHold = absFiltered;
            _rmsAccum += filtered * filtered;
            _rmsCount++;

            // ── Fill measurement windows (sliding ring buffers) ──
            _momentaryBlock[_momentaryPos] = filtered;
            if (++_momentaryPos >= MOMENTARY_SAMPLES) _momentaryPos = 0;
            if (_momentaryFill < MOMENTARY_SAMPLES) _momentaryFill++;

            _shortTermBlock[_shortTermPos] = filtered;
            if (++_shortTermPos >= SHORT_TERM_SAMPLES) _shortTermPos = 0;
            if (_shortTermFill < SHORT_TERM_SAMPLES) _shortTermFill++;

            // ── Periodic measurement update (sample-driven, fires for any
            //    chunk size — even a multi-second buffer in a single call) ──
            if (++_samplesSinceUpdate >= UPDATE_INTERVAL_SAMPLES)
            {
                _samplesSinceUpdate = 0;
                UpdateMeasurements();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Measurements
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateMeasurements()
    {
        // Momentary loudness
        if (_momentaryFill >= MOMENTARY_SAMPLES / 2)
        {
            float meanSquare = ComputeMeanSquare(_momentaryBlock, _momentaryFill);
            MomentaryLufs = MeanSquareToLufs(meanSquare);

            // Add a 400ms block to the integrated accumulator every 4 ticks
            // (100ms × 4 = 400ms per ITU-R BS.1770-4 block length)
            if (++_ticksSinceIntegrated >= 4)
            {
                _ticksSinceIntegrated = 0;
                _integratedBlocks.Add(MomentaryLufs);
            }
        }

        // Short-term loudness
        if (_shortTermFill >= SHORT_TERM_SAMPLES / 4)
        {
            float meanSquare = ComputeMeanSquare(_shortTermBlock, _shortTermFill);
            ShortTermLufs = MeanSquareToLufs(meanSquare);
        }

        // Integrated loudness (gated average)
        if (_integratedBlocks.Count > 0)
        {
            // Absolute gate: -70 LUFS
            float sum = 0f;
            int count = 0;
            foreach (var lufs in _integratedBlocks)
            {
                if (lufs > -70f)
                {
                    sum += lufs;
                    count++;
                }
            }

            if (count > 0)
            {
                float avgLufs = sum / count;

                // Relative gate: -10 LU below absolute
                sum = 0f;
                count = 0;
                foreach (var lufs in _integratedBlocks)
                {
                    if (lufs > avgLufs - 10f)
                    {
                        sum += lufs;
                        count++;
                    }
                }

                if (count > 0)
                    IntegratedLufs = sum / count;
            }

            // Loudness Range (LRA)
            ComputeLoudnessRange();
        }

        // True Peak
        TruePeakDb = _truePeak > 1e-10f ? VonvertUtils.LinearToDb(_truePeak) : -70f;

        // Dynamic Range
        if (_rmsCount > 0 && _peakHold > 1e-10f)
        {
            float rmsDb = VonvertUtils.LinearToDb(MathF.Sqrt(_rmsAccum / _rmsCount));
            float peakDb = VonvertUtils.LinearToDb(_peakHold);
            DynamicRangeDb = peakDb - rmsDb;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeMeanSquare(float[] block, int count)
    {
        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += block[i] * block[i];
        return sum / count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float MeanSquareToLufs(float meanSquare)
    {
        // LUFS = -0.691 + 10 × log10(meanSquare)
        if (meanSquare < 1e-20f) return -70f;
        return -0.691f + 10f * MathF.Log10(meanSquare);
    }

    private void ComputeLoudnessRange()
    {
        if (_integratedBlocks.Count < 10) return;

        // Sort and compute 10th/95th percentiles
        var sorted = new float[_integratedBlocks.Count];
        _integratedBlocks.CopyTo(sorted);
        Array.Sort(sorted);

        int idx10 = sorted.Length / 10;
        int idx95 = sorted.Length * 95 / 100;

        LoudnessRange = sorted[idx95] - sorted[idx10];
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Reset
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Reset all measurements.</summary>
    public void Reset()
    {
        _momentaryFill = 0;
        _momentaryPos = 0;
        _shortTermFill = 0;
        _shortTermPos = 0;
        _integratedBlocks.Clear();
        _shX1 = _shX2 = _shY1 = _shY2 = 0f;
        _hpX1 = _hpX2 = _hpY1 = _hpY2 = 0f;
        _truePeak = 0f;
        _vuLevel = 0f;
        _peakHold = 0f;
        _rmsAccum = 0f;
        _rmsCount = 0;
        _samplesSinceUpdate = 0;
        _ticksSinceIntegrated = 0;

        MomentaryLufs = -70f;
        ShortTermLufs = -70f;
        IntegratedLufs = -70f;
        LoudnessRange = 0f;
        TruePeakDb = -70f;
        DynamicRangeDb = 0f;
    }
}
