// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Modulation delay — the incoming signal's own amplitude modulates the delay
 * time, so loud passages wobble more than quiet ones. That signal-coupled
 * motion reads as "underwater", "tape warble" or a warm chorus-like thickening.
 *
 * LFO-driven modulation (Chorus, Flanger, Vibrato) is periodic and independent
 * of the source; this effect tracks the source envelope instead, which keeps
 * the wobble in sync with speech dynamics.
 *
 * Signal path: envelope follower (2 ms attack / 20 ms release) → delay-time
 * offset in fractional samples → linear-interpolated delay-line read →
 * feedback → dry/wet mix.
 *
 * All buffers pre-allocated; no heap allocation inside Process().
 */
public sealed class ModulationDelayEffect : IAudioEffect
{
    public string Name      { get; } = "ModulationDelay";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _baseDelayMs  = 10f;
    private float _modDepth     = 0.5f;
    private float _feedback     = 0.3f;
    private float _mix          = 0.4f;

    /// <summary>Base delay time in ms (1 – 50). Default: 10.</summary>
    public float BaseDelayMs
    {
        get => _baseDelayMs;
        set => _baseDelayMs = Math.Clamp(value, 1f, 50f);
    }

    /// <summary>Modulation depth (0 – 1). Default: 0.5.</summary>
    public float ModDepth
    {
        get => _modDepth;
        set => _modDepth = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Feedback amount (0 – 0.85). Default: 0.3.</summary>
    public float Feedback
    {
        get => _feedback;
        set => _feedback = Math.Clamp(value, 0f, 0.85f);
    }

    /// <summary>Wet/dry mix (0 – 1). Default: 0.4.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const int SAMPLE_RATE    = AudioConstants.EngineRate;
    private const int DELAY_LEN      = 4096;   // ~85 ms @ 48 kHz (power-of-2)
    private const int DELAY_MASK     = DELAY_LEN - 1;
    private const float MAX_MOD_SAMP = 480f;   // 10 ms max modulation range

    private readonly float[] _delayLine = new float[DELAY_LEN];
    private int   _writePos = 0;
    private float _envelope = 0f;

    // Envelope smoothing
    private readonly float _attackCoef = MathF.Exp(-1f / (0.002f * SAMPLE_RATE));  // 2 ms
    private readonly float _releaseCoef = MathF.Exp(-1f / (0.020f * SAMPLE_RATE)); // 20 ms

    public void Process(Span<float> buf)
    {
        float baseSamp = _baseDelayMs * SAMPLE_RATE / 1000f;
        float depth    = _modDepth;
        float fb       = _feedback;
        float wet      = _mix;
        float dry      = 1f - wet;

        for (int i = 0; i < buf.Length; i++)
        {
            float input = buf[i];

            // Track input envelope
            float mag = MathF.Abs(input);
            _envelope = mag > _envelope
                ? _attackCoef * (_envelope - mag) + mag
                : _releaseCoef * (_envelope - mag) + mag;

            // Modulate delay time by envelope
            float floatDelay = baseSamp + _envelope * depth * MAX_MOD_SAMP;
            floatDelay = Math.Clamp(floatDelay, 1f, DELAY_LEN - 2f);

            // Fractional delay read (linear interpolation)
            int   d0   = (int)floatDelay;
            float frac = floatDelay - d0;
            int r0 = (_writePos - d0     + DELAY_LEN) & DELAY_MASK;
            int r1 = (_writePos - d0 - 1 + DELAY_LEN) & DELAY_MASK;
            float delayed = _delayLine[r0] + frac * (_delayLine[r1] - _delayLine[r0]);

            // Write input + feedback
            _delayLine[_writePos] = input + delayed * fb;

            // Output mix
            buf[i] = input * dry + delayed * wet;

            _writePos = (_writePos + 1) & DELAY_MASK;
        }
    }

    public void Reset()
    {
        Array.Clear(_delayLine);
        _writePos = 0;
        _envelope = 0f;
    }
}
