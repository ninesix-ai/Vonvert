// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/// <summary>
/// Tremolo — periodic amplitude modulation at 0.5–20 Hz.
/// Supports sine, triangle and square LFO waveforms.
/// Zero-alloc, single-phase state.
/// </summary>
public sealed class TremoloEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceTremolo";
    public bool   IsEnabled { get; set; }

    private const float SAMPLE_RATE = AudioConstants.EngineRate;

    private float _rate = 4f;
    /// <summary>Modulation frequency in Hz. Range: 0.5–20, default: 4.</summary>
    public float Rate
    {
        get => _rate;
        set => _rate = Math.Clamp(value, 0.5f, 20f);
    }

    private float _depth = 0.5f;
    /// <summary>Modulation depth. 0 = none, 1 = full cut-to-silence. Default: 0.5.</summary>
    public float Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>LFO waveform shape.</summary>
    public enum WaveformKind { Sine, Triangle, Square }

    private WaveformKind _waveform = WaveformKind.Sine;
    public WaveformKind Waveform
    {
        get => _waveform;
        set => _waveform = value;
    }

    private float _phase = 0f;

    public void Process(Span<float> buf)
    {
        float phaseInc = _rate / SAMPLE_RATE;
        float depth    = _depth;

        for (int i = 0; i < buf.Length; i++)
        {
            float lfo;
            switch (_waveform)
            {
                case WaveformKind.Triangle:
                    float t = _phase < 0.5f ? _phase * 2f : 2f - _phase * 2f;
                    lfo = t;
                    break;

                case WaveformKind.Square:
                    lfo = _phase < 0.5f ? 1f : 0f;
                    break;

                default:
                    lfo = 0.5f + 0.5f * MathF.Sin(MathF.Tau * _phase);
                    break;
            }

            buf[i] *= 1f - depth * lfo;

            _phase += phaseInc;
            if (_phase >= 1f) _phase -= 1f;
        }
    }

    public void Reset()
    {
        _phase = 0f;
    }
}
