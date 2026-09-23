// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/// <summary>
/// Vibrato — pure pitch modulation via an LFO-driven fractional delay.
///
/// Unlike Chorus (which uses longer delays for spatial depth), Vibrato uses a
/// very short delay (≈ 2–6 ms) swept by a deep LFO to create a perceptible
/// pitch wobble — a "trembling" / "quavering" voice quality.
///
///   - Variable rate (0.5 – 12 Hz), depth (0 – 1) and wet/dry mix.
///   - Fractional delay via linear interpolation (no quantisation noise).
///
/// Buffers are pre-allocated so Process() is zero-alloc on the DSP thread.
/// </summary>
public sealed class VibratoEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceVibrato";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _rate  = 5.0f;
    private float _depth = 0.4f;
    private float _mix   = 1.0f;

    /// <summary>LFO rate in Hz (0.5 – 12). Default: 5.</summary>
    public float Rate
    {
        get => _rate;
        set => _rate = Math.Clamp(value, 0.5f, 12f);
    }

    /// <summary>Modulation depth (0 – 1). Default: 0.4.</summary>
    public float Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Wet/dry mix (0 – 1). Default: 1.0 (fully wet).</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>LFO waveform shape.</summary>
    public enum VibratoWaveform { Sine, Triangle, Square, Random }

    private VibratoWaveform _waveform = VibratoWaveform.Sine;
    public VibratoWaveform Waveform
    {
        get => _waveform;
        set => _waveform = value;
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const int SAMPLE_RATE   = AudioConstants.EngineRate;
    private const int DELAY_LEN     = 1024;   // ~21 ms @ 48 kHz (power-of-2)
    private const int DELAY_MASK    = DELAY_LEN - 1;
    private const float MAX_DEPTH_SAMP = 192f; // 4 ms max modulation depth

    private readonly float[] _delayLine = new float[DELAY_LEN];
    private int   _writePos = 0;
    private float _lfoPhase   = 0f;
    private float _lastRandom = 0f;

    public void Process(Span<float> buf)
    {
        float lfoInc = _rate / SAMPLE_RATE;
        float wet    = _mix;
        float dry    = 1f - wet;
        float depth  = _depth;

        for (int i = 0; i < buf.Length; i++)
        {
            // Generate LFO value (0..1)
            float lfo = GetLfoValue(_lfoPhase);

            // Fractional delay: base delay + LFO modulation
            float baseDelay = MAX_DEPTH_SAMP * 0.5f;  // 2 ms base
            float floatDelay = baseDelay + lfo * depth * MAX_DEPTH_SAMP;
            int   d0   = (int)floatDelay;
            float frac = floatDelay - d0;

            int r0 = (_writePos - d0     + DELAY_LEN) & DELAY_MASK;
            int r1 = (_writePos - d0 - 1 + DELAY_LEN) & DELAY_MASK;
            float delayed = _delayLine[r0] + frac * (_delayLine[r1] - _delayLine[r0]);

            _delayLine[_writePos] = buf[i];
            buf[i] = buf[i] * dry + delayed * wet;

            _writePos = (_writePos + 1) & DELAY_MASK;
            _lfoPhase = (_lfoPhase + lfoInc) % 1f;
        }
    }

    private float GetLfoValue(float phase)
    {
        switch (_waveform)
        {
            case VibratoWaveform.Triangle:
                return phase < 0.5f ? phase * 4f - 1f : 3f - phase * 4f;
            case VibratoWaveform.Square:
                return phase < 0.5f ? 1f : -1f;
            case VibratoWaveform.Random:
                // Sample-and-hold random: new value each LFO cycle
                if (phase < _lfoPhase) _lastRandom = Random.Shared.NextSingle() * 2f - 1f;
                return _lastRandom;
            default: // Sine
                return MathF.Sin(MathF.Tau * phase);
        }
    }

    public void Reset()
    {
        Array.Clear(_delayLine);
        _writePos = 0;
        _lfoPhase = 0f;
        _lastRandom = 0f;
    }
}
