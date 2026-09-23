// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/// <summary>
/// Flanger — a short delay line (0.5–5 ms) swept by an LFO with feedback,
/// producing the classic "jet-plane" comb-filter sweep.
/// Fractional delay via linear interpolation removes LFO quantisation artifacts.
/// The delay buffer is pre-allocated so Process() is zero-alloc on the DSP thread.
/// </summary>
public sealed class FlangerEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceFlanger";
    public bool   IsEnabled { get; set; }

    private const float SAMPLE_RATE = AudioConstants.EngineRate;
    private const int   DELAY_LEN   = 4096;   // power-of-2 for fast wrap (≥ 50 ms @ 48 kHz)
    private const int   DELAY_MASK  = DELAY_LEN - 1;
    private const float MIN_DELAY   = 24f;    // 0.5 ms
    private const float MAX_DELAY   = 240f;   // 5.0 ms

    private float _rate = 0.5f;
    /// <summary>LFO frequency in Hz. Range: 0.1–10, default: 0.5.</summary>
    public float Rate
    {
        get => _rate;
        set => _rate = Math.Clamp(value, 0.1f, 10f);
    }

    private float _depth = 0.5f;
    /// <summary>LFO modulation depth. 0 = no modulation, 1 = full sweep. Default: 0.5.</summary>
    public float Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0f, 1f);
    }

    private float _feedback = 0.5f;
    /// <summary>Feedback amount. Range: -0.95 to 0.95. Default: 0.5.</summary>
    public float Feedback
    {
        get => _feedback;
        set => _feedback = Math.Clamp(value, -0.95f, 0.95f);
    }

    private float _mix = 0.5f;
    /// <summary>Wet/dry mix. 0 = dry, 1 = fully wet. Default: 0.5.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    private readonly float[] _delay = new float[DELAY_LEN];
    private int   _writePos = 0;
    private float _lfoPhase = 0f;

    public void Process(Span<float> buf)
    {
        float lfoInc = _rate / SAMPLE_RATE;
        float wet    = _mix;
        float dry    = 1f - wet;
        float fb     = _feedback;
        float depth  = _depth;

        for (int i = 0; i < buf.Length; i++)
        {
            // Fractional delay: continuous LFO → sub-sample read position
            float lfo        = 0.5f + 0.5f * MathF.Sin(MathF.Tau * _lfoPhase);
            float floatDelay = MIN_DELAY + lfo * depth * (MAX_DELAY - MIN_DELAY);
            int   d0         = (int)floatDelay;                       // integer part
            float frac       = floatDelay - d0;                       // fractional part

            // Two adjacent read positions with power-of-2 wrap
            int r0 = (_writePos - d0     + DELAY_LEN) & DELAY_MASK;
            int r1 = (_writePos - d0 - 1 + DELAY_LEN) & DELAY_MASK;

            // Linear interpolation between adjacent samples
            float delayed = _delay[r0] + frac * (_delay[r1] - _delay[r0]);

            _delay[_writePos] = buf[i] + delayed * fb;
            buf[i] = buf[i] * dry + delayed * wet;

            _writePos = (_writePos + 1) & DELAY_MASK;
            _lfoPhase = (_lfoPhase + lfoInc) % 1f;
        }
    }

    public void Reset()
    {
        Array.Clear(_delay);
        _writePos = 0;
        _lfoPhase = 0f;
    }
}
