// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/// <summary>
/// Phaser — cascaded first-order allpass filters swept by an LFO, with feedback.
/// Places moving notches in the spectrum to create the sweeping "whoosh" effect.
///
/// Algorithm:
///   1. An LFO modulates the allpass coefficient (cutoff).
///   2. The signal passes through N cascaded one-pole allpass stages.
///   3. Dry + wet (allpass output) are mixed, then fed back into the input.
///
/// State arrays are pre-allocated so Process() is zero-alloc on the DSP thread.
/// </summary>
public sealed class PhaserEffect : IAudioEffect
{
    public string Name      { get; } = "VoicePhaser";
    public bool   IsEnabled { get; set; }

    private const float SAMPLE_RATE = AudioConstants.EngineRate;
    private const int   MAX_STAGES  = 8;

    private float _rate = 1.0f;
    /// <summary>LFO sweep rate in Hz. Range: 0.1–8, default: 1.</summary>
    public float Rate
    {
        get => _rate;
        set => _rate = Math.Clamp(value, 0.1f, 8f);
    }

    private float _depth = 0.7f;
    /// <summary>Sweep depth. 0 = no sweep, 1 = full range. Default: 0.7.</summary>
    public float Depth
    {
        get => _depth;
        set => _depth = Math.Clamp(value, 0f, 1f);
    }

    private int _stages = 6;
    /// <summary>Number of allpass stages (2/4/6/8). More = deeper notches. Default: 6.</summary>
    public int Stages
    {
        get => _stages;
        set => _stages = (int)Math.Clamp(value, 2, MAX_STAGES);
    }

    private float _feedback = 0.5f;
    /// <summary>Feedback from output back to input. Range: -0.95 to 0.95. Default: 0.5.</summary>
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

    private readonly float[] _apState = new float[MAX_STAGES];
    private float _lfoPhase = 0f;

    public void Process(Span<float> buf)
    {
        float lfoInc = _rate / SAMPLE_RATE;
        int   nStages = _stages;
        float fb      = _feedback;
        float wet     = _mix;
        float dry     = 1f - wet;
        float depth   = _depth;

        for (int i = 0; i < buf.Length; i++)
        {
            float lfo   = 0.5f + 0.5f * MathF.Sin(MathF.Tau * _lfoPhase);
            float coeff = 0.1f + depth * lfo * 0.8f;

            float input = buf[i] + _apState[nStages - 1] * fb;

            float x = input;
            for (int s = 0; s < nStages; s++)
            {
                float y = coeff * (x - _apState[s]) + _apState[s];
                _apState[s] = y;
                x = y;
            }

            buf[i] = buf[i] * dry + x * wet;

            _lfoPhase += lfoInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
        }
    }

    public void Reset()
    {
        Array.Clear(_apState);
        _lfoPhase = 0f;
    }
}
