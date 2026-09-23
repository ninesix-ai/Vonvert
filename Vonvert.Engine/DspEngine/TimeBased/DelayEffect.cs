// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Runtime.CompilerServices;

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Stereo-width delay / echo effect with feedback and ping-pong mode.
 *
 * Features:
 *   - Variable delay time (10 ms – 1500 ms).
 *   - Feedback control (0 – 95 %) for single echo → infinite decay.
 *   - Wet / dry mix.
 *   - Ping-pong mode: alternates echo between L/R channels (pseudo-stereo
 *     from mono input — the delayed sample is panned left then right).
 *   - Low-pass damping on feedback path to simulate natural decay.
 *
 * All buffers are pre-allocated. Process() is zero-alloc on the DSP thread.
 */
public sealed class DelayEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceDelay";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────
    private float _timeMs = 300f;
    private float _feedback = 0.35f;
    private float _mix = 0.3f;
    private float _damping = 0.5f;     // LP on feedback (0=bright, 1=dark)
    private bool  _pingPong = false;

    /// <summary>Delay time in milliseconds (10 – 1500).</summary>
    public float TimeMs
    {
        get => _timeMs;
        set => _timeMs = Math.Clamp(value, 10f, 1500f);
    }

    /// <summary>Feedback amount 0 – 0.95.</summary>
    public float Feedback
    {
        get => _feedback;
        set => _feedback = Math.Clamp(value, 0f, 0.95f);
    }

    /// <summary>Wet/dry mix 0 – 1.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Feedback low-pass damping 0 (bright) – 1 (dark).</summary>
    public float Damping
    {
        get => _damping;
        set => _damping = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Ping-pong mode: alternates echo L/R for pseudo-stereo.</summary>
    public bool PingPong
    {
        get => _pingPong;
        set => _pingPong = value;
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const int MAX_DELAY_SAMPLES = 131072; // 131072 = 2^17, covers >1.5s @ 48kHz
    private const int DELAY_MASK = MAX_DELAY_SAMPLES - 1; // bitwise wrap for power-of-2 size
    private const int SAMPLE_RATE = 48000;

    private readonly float[] _delayLine = new float[MAX_DELAY_SAMPLES];
    private int   _writePos  = 0;
    private float _lpState   = 0f;       // one-pole LP filter state
    private float _ppPhase   = 0f;       // ping-pong LFO phase
    private float _ppModPhase = 0f;      // ping-pong delay modulation phase

    public void Process(Span<float> buf)
    {
        float delaySampF = _timeMs * SAMPLE_RATE / 1000f;
        int   delaySamples = (int)delaySampF;
        delaySamples = Math.Clamp(delaySamples, 1, MAX_DELAY_SAMPLES - 1);

        float fb   = _feedback;
        float wet  = _mix;
        float dry  = 1f - wet;
        float damp = _damping;
        float lpA  = damp;              // coeff: y = a*x + (1-a)*y_prev
        float lpB  = 1f - damp;
        bool  pp   = _pingPong;

        // Ping-pong LFO: rate = 1 cycle per delay period
        float ppLfoInc = pp ? 1f / delaySamples : 0f;
        // Delay modulation LFO: slower, creates subtle pitch movement
        float ppModInc = pp ? ppLfoInc * 0.25f : 0f;

        for (int i = 0; i < buf.Length; i++)
        {
            float delayed;

            if (pp)
            {
                // Improved ping-pong: smooth amplitude + delay time modulation
                // Amplitude: sine LFO creates smooth “L/R” panning illusion
                float ampMod = 0.7f + 0.3f * MathF.Sin(MathF.Tau * _ppPhase);

                // Delay modulation: ±3% variation for spatial movement
                float delayMod = MathF.Sin(MathF.Tau * _ppModPhase) * delaySampF * 0.03f;
                float modDelay = delaySampF + delayMod;
                int   d0       = (int)modDelay;
                float frac     = modDelay - d0;

                // Fractional delay read (linear interpolation)
                int r0 = (_writePos - d0     + MAX_DELAY_SAMPLES) & DELAY_MASK;
                int r1 = (_writePos - d0 - 1 + MAX_DELAY_SAMPLES) & DELAY_MASK;
                delayed = (_delayLine[r0] + frac * (_delayLine[r1] - _delayLine[r0])) * ampMod;

                _ppPhase    = (_ppPhase    + ppLfoInc) % 1f;
                _ppModPhase = (_ppModPhase + ppModInc) % 1f;
            }
            else
            {
                int readPos = _writePos - delaySamples;
                if (readPos < 0) readPos += MAX_DELAY_SAMPLES;
                delayed = _delayLine[readPos];
            }

            // Write input + damped feedback into delay line
            _lpState = lpA * buf[i] + lpB * _lpState;
            _delayLine[_writePos] = buf[i] + _lpState * fb;

            // Output mix
            buf[i] = buf[i] * dry + delayed * wet;

            _writePos = (_writePos + 1) & DELAY_MASK;
        }
    }

    public void Reset()
    {
        Array.Clear(_delayLine);
        _writePos   = 0;
        _lpState    = 0f;
        _ppPhase    = 0f;
        _ppModPhase = 0f;
    }
}
