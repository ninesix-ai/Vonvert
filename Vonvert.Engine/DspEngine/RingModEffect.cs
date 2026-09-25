// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.Engine.DspEngine;

/*
 * Ring modulator — multiplies the input by an internal carrier oscillator,
 * producing sum and difference frequencies (f_in ± f_carrier) instead of the
 * harmonically related content a pitch shifter creates.
 *
 * Controls:
 *   - CarrierFreq:   carrier pitch, 10–2000 Hz
 *   - Mix:           dry/wet blend, 0 = unprocessed, 1 = fully modulated
 *   - HarmonicDepth: adds odd-harmonic content to the carrier, broadening the
 *                    timbre from flute-like (0) toward metallic (1)
 *   - Waveform:      carrier shape (Sine / Square / Sawtooth / Triangle)
 *
 * The result is a metallic, bell-like or robotic timbre; the closely related
 * VxRobot uses a fixed sine carrier and no mix control.
 *
 * All state pre-allocated; no heap allocation inside Process().
 */
public sealed class RingModEffect : IAudioEffect
{
    public string Name      { get; } = "RingMod";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _carrierFreq = 80f;
    /// <summary>Carrier frequency in Hz (10–2000). Default: 80.</summary>
    public float CarrierFreq
    {
        get => _carrierFreq;
        set => _carrierFreq = Math.Clamp(value, 10f, 2000f);
    }

    private float _mix = 0.5f;
    /// <summary>Wet/dry mix 0–1. Default: 0.5.</summary>
    public float Mix
    {
        get => _mix;
        set => _mix = Math.Clamp(value, 0f, 1f);
    }

    private float _harmonicDepth = 0f;
    /// <summary>
    /// Harmonic richness 0–1. At 0, pure sine carrier.
    /// At 1, carrier includes stronger odd harmonics (more aggressive timbre).
    /// Default: 0.
    /// </summary>
    public float HarmonicDepth
    {
        get => _harmonicDepth;
        set => _harmonicDepth = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Carrier waveform shape.</summary>
    public enum CarrierWaveform { Sine, Square, Sawtooth, Triangle }

    private CarrierWaveform _waveform = CarrierWaveform.Sine;
    /// <summary>Carrier waveform. Default: Sine.</summary>
    public CarrierWaveform Waveform
    {
        get => _waveform;
        set => _waveform = value;
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const float SAMPLE_RATE = 48000f;
    private float _phase = 0f;

    public void Process(Span<float> buf)
    {
        float phaseInc = _carrierFreq / SAMPLE_RATE;
        float wet  = _mix;
        float dry  = 1f - wet;
        float hd   = _harmonicDepth;

        for (int i = 0; i < buf.Length; i++)
        {
            float carrier = GenerateCarrier(_phase, hd);

            // Ring modulation: multiply input by carrier
            float modulated = buf[i] * carrier;

            // Dry/wet mix
            buf[i] = buf[i] * dry + modulated * wet;

            // Advance oscillator
            _phase += phaseInc;
            if (_phase >= 1f) _phase -= 1f;
        }
    }

    /// <summary>Generate carrier sample from phase (0..1) and harmonic depth.</summary>
    private float GenerateCarrier(float phase, float harmonicDepth)
    {
        float tau_phase = MathF.Tau * phase;

        switch (_waveform)
        {
            case CarrierWaveform.Square:
            {
                float square = phase < 0.5f ? 1f : -1f;
                // Add harmonics via band-limited approximation
                if (harmonicDepth > 0.01f)
                {
                    float h3 = MathF.Sin(tau_phase * 3f) / 3f;
                    float h5 = MathF.Sin(tau_phase * 5f) / 5f;
                    square += harmonicDepth * (h3 * 0.5f + h5 * 0.3f);
                }
                return Math.Clamp(square, -1.5f, 1.5f);
            }

            case CarrierWaveform.Sawtooth:
            {
                // Naive sawtooth: 2*phase - 1
                float saw = 2f * phase - 1f;
                if (harmonicDepth > 0.01f)
                {
                    // Soften with harmonics
                    float h2 = MathF.Sin(tau_phase * 2f) * 0.3f;
                    float h3 = MathF.Sin(tau_phase * 3f) * 0.2f;
                    saw += harmonicDepth * (h2 + h3);
                }
                return saw;
            }

            case CarrierWaveform.Triangle:
            {
                float tri = phase < 0.5f
                    ? 4f * phase - 1f
                    : 3f - 4f * phase;
                if (harmonicDepth > 0.01f)
                {
                    // Triangle has odd harmonics at 1/n²
                    float h3 = MathF.Sin(tau_phase * 3f) / 9f;
                    float h5 = MathF.Sin(tau_phase * 5f) / 25f;
                    tri -= harmonicDepth * (h3 + h5) * 2f;
                }
                return tri;
            }

            default: // Sine
            {
                float sine = MathF.Sin(tau_phase);
                if (harmonicDepth > 0.01f)
                {
                    // Add odd harmonics for richer sine
                    float h3 = MathF.Sin(tau_phase * 3f) * 0.3f;
                    float h5 = MathF.Sin(tau_phase * 5f) * 0.15f;
                    sine += harmonicDepth * (h3 + h5);
                }
                return sine;
            }
        }
    }

    public void Reset()
    {
        _phase = 0f;
    }
}
