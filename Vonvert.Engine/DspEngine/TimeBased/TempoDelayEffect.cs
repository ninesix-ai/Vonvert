// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Note-division enum for tempo-synced delay.
 * Values represent the delay time as a fraction of one beat (quarter note).
 */
public enum TempoNoteDivision
{
    Whole           = 0,    // 4 beats
    HalfDotted      = 1,    // 3 beats
    Half            = 2,    // 2 beats
    QuarterDotted   = 3,    // 1.5 beats
    Quarter         = 4,    // 1 beat
    EighthDotted    = 5,    // 0.75 beats
    QuarterTriplet  = 6,    // 2/3 beat
    Eighth          = 7,    // 0.5 beats
    EighthTriplet   = 8,    // 1/3 beat
    SixteenthDotted = 9,    // 0.375 beats
    Sixteenth       = 10    // 0.25 beats
}

/*
 * Tempo-synced delay — echo effect with delay time locked to musical
 * subdivisions of the current BPM.
 *
 * Features:
 *   - BPM-locked delay time (30 – 300 BPM).
 *   - Musical note divisions: whole → sixteenth notes, including dotted
 *     and triplet variants for rhythmic complexity.
 *   - Fractional delay via linear interpolation (no LFO quantisation noise).
 *   - Feedback low-pass damping for natural decay darkening.
 *   - Wet/dry mix control.
 *
 * Max delay: 2000 ms (whole note @ 120 BPM) = 96 000 samples @ 48 kHz.
 * Buffer: 131 072 = 2^17 — covers >2.7 s with power-of-2 fast wrap.
 *
 * All buffers pre-allocated. Zero GC on DSP thread.
 */
public sealed class TempoDelayEffect : IAudioEffect
{
    public string Name      { get; } = "TempoDelay";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _bpm = 120f;
    private TempoNoteDivision _division = TempoNoteDivision.Eighth;
    private float _feedback  = 0.4f;
    private float _damping   = 0.3f;
    private float _mix       = 0.35f;

    /// <summary>Tempo in BPM (30 – 300). Default: 120.</summary>
    public float Bpm
    {
        get => _bpm;
        set { _bpm = Math.Clamp(value, 30f, 300f); _dirty = true; }
    }

    /// <summary>Note division for delay time. Default: Eighth.</summary>
    public TempoNoteDivision Division
    {
        get => _division;
        set { _division = value; _dirty = true; }
    }

    /// <summary>Feedback amount (0 – 0.9). Default: 0.4.</summary>
    public float Feedback
    {
        get => _feedback;
        set => _feedback = Math.Clamp(value, 0f, 0.9f);
    }

    /// <summary>Feedback low-pass damping (0 = bright, 1 = dark). Default: 0.3.</summary>
    public float Damping
    {
        get => _damping;
        set => _damping = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Wet/dry mix (0 – 1). Default: 0.35.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Current delay time in milliseconds (read-only, derived from BPM + division).</summary>
    public float DelayTimeMs => GetDelayTimeMs(_bpm, _division);

    // ── Internal state ────────────────────────────────────────────────────

    private const int SAMPLE_RATE        = AudioConstants.EngineRate;
    private const int MAX_DELAY_SAMPLES  = 131072;   // 2^17 ≈ 2.73 s
    private const int DELAY_MASK         = MAX_DELAY_SAMPLES - 1;

    private readonly float[] _delayLine = new float[MAX_DELAY_SAMPLES];
    private int   _writePos = 0;
    private float _lpState  = 0f;

    // Cached delay-in-samples (recalculated when BPM or division changes)
    private float _delaySamples = 24000f;   // 0.5 s default
    private volatile bool _dirty = true;

    public void Process(Span<float> buf)
    {
        if (_dirty) RecalcDelay();

        float delaySamp = _delaySamples;
        int   d0        = (int)delaySamp;
        float frac      = delaySamp - d0;

        float wet   = _mix;
        float dry   = 1f - wet;
        float fb    = _feedback;
        float damp  = _damping;
        float lpA   = damp;        // one-pole LP: y = a*x + (1-a)*y_prev
        float lpB   = 1f - damp;

        for (int i = 0; i < buf.Length; i++)
        {
            // Fractional delay read (linear interpolation)
            int r0 = (_writePos - d0     + MAX_DELAY_SAMPLES) & DELAY_MASK;
            int r1 = (_writePos - d0 - 1 + MAX_DELAY_SAMPLES) & DELAY_MASK;
            float delayed = _delayLine[r0] + frac * (_delayLine[r1] - _delayLine[r0]);

            // Write input + damped feedback into delay line
            _lpState = lpA * buf[i] + lpB * _lpState;
            _delayLine[_writePos] = buf[i] + _lpState * fb;

            // Output mix
            buf[i] = buf[i] * dry + delayed * wet;

            _writePos = (_writePos + 1) & DELAY_MASK;
        }
    }

    private void RecalcDelay()
    {
        _delaySamples = GetDelayTimeMs(_bpm, _division) * SAMPLE_RATE / 1000f;
        _delaySamples = Math.Clamp(_delaySamples, 1f, MAX_DELAY_SAMPLES - 1f);
        _dirty = false;
    }

    /// <summary>Convert BPM + note division to delay time in milliseconds.</summary>
    public static float GetDelayTimeMs(float bpm, TempoNoteDivision division)
    {
        float beatMs = 60000f / bpm;   // one quarter note in ms
        return division switch
        {
            TempoNoteDivision.Whole           => beatMs * 4f,
            TempoNoteDivision.HalfDotted      => beatMs * 3f,
            TempoNoteDivision.Half            => beatMs * 2f,
            TempoNoteDivision.QuarterDotted   => beatMs * 1.5f,
            TempoNoteDivision.Quarter         => beatMs,
            TempoNoteDivision.EighthDotted    => beatMs * 0.75f,
            TempoNoteDivision.QuarterTriplet  => beatMs * 2f / 3f,
            TempoNoteDivision.Eighth          => beatMs * 0.5f,
            TempoNoteDivision.EighthTriplet   => beatMs / 3f,
            TempoNoteDivision.SixteenthDotted => beatMs * 0.375f,
            TempoNoteDivision.Sixteenth       => beatMs * 0.25f,
            _ => beatMs
        };
    }

    public void Reset()
    {
        Array.Clear(_delayLine);
        _writePos = 0;
        _lpState  = 0f;
    }
}
