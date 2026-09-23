// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Runtime.CompilerServices;

namespace Vonvert.Engine.DspEngine;

/*
 * De-esser — detect and attenuate sibilant consonants (s, sh, ch, z, zh)
 * that occupy the 4–10 kHz region.
 *
 * Algorithm:
 *   1. Bandpass-filter the signal around the sibilance frequency using two
 *      cascaded peaking EQ filters (high-Q boost) to create a "sidechain"
 *      that emphasises sibilant energy.
 *   2. Track the envelope of this sidechain with attack/release smoothing.
 *   3. When the envelope exceeds the threshold, apply gain reduction to the
 *      sibilance band only (not the full signal), preserving natural tone.
 *
 * Parameters:
 *   - Frequency: centre of the sibilance band (default 6 kHz)
 *   - Threshold: dB level at which de-essing engages (default -18 dB)
 *   - Ratio: compression ratio in the sibilance band (default 4:1)
 *   - BandwidthQ: Q of the detection filter (default 2.0)
 *
 * Zero-alloc, time-domain only. Uses the existing BiquadFilter class.
 */
public sealed class DeesserEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceDeesser";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _frequency = 6000f;
    private float _thresholdDb = -18f;
    private float _ratio = 4f;
    private float _q = 2.0f;

    /// <summary>Sibilance centre frequency (2000 – 12000 Hz).</summary>
    public float Frequency
    {
        get => _frequency;
        set { _frequency = Math.Clamp(value, 2000f, 12000f); _dirty = true; }
    }

    /// <summary>Threshold in dB at which de-essing engages (-40 to 0).</summary>
    public float ThresholdDb
    {
        get => _thresholdDb;
        set => _thresholdDb = Math.Clamp(value, -40f, 0f);
    }

    /// <summary>Compression ratio in the sibilance band (1 – 20).</summary>
    public float Ratio
    {
        get => _ratio;
        set => _ratio = Math.Clamp(value, 1f, 20f);
    }

    /// <summary>Bandpass Q factor (0.5 – 8.0). Higher = narrower band.</summary>
    public float BandwidthQ
    {
        get => _q;
        set { _q = Math.Clamp(value, 0.5f, 8.0f); _dirty = true; }
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const float FS = 48000f;

    // Detection band: peaking EQ boost to isolate sibilance
    private readonly BiquadFilter _detectHi = new();  // peaking at _frequency
    private readonly BiquadFilter _detectLo = new();  // peaking at _frequency * 0.7

    // Envelope follower
    private float _envelope = 0f;
    private readonly float _attackCoef = MathF.Exp(-1f / (0.003f * FS));  // 3 ms attack
    private readonly float _releaseCoef = MathF.Exp(-1f / (0.080f * FS)); // 80 ms release

    // Coefficient recalculation flag
    private volatile bool _dirty = true;

    public void Process(Span<float> buf)
    {
        if (_dirty) RecalcFilters();

        float thresh = MathF.Pow(10f, _thresholdDb / 20f);
        float ratio = _ratio;

        for (int i = 0; i < buf.Length; i++)
        {
            float input = buf[i];

            // Extract sibilance energy via bandpass detection
            float sibilance = _detectHi.Process(input);
            sibilance = _detectLo.Process(sibilance);

            // Envelope tracking (peak-hold with attack/release)
            float mag = MathF.Abs(sibilance);
            _envelope = mag > _envelope
                ? _attackCoef * (_envelope - mag) + mag
                : _releaseCoef * (_envelope - mag) + mag;

            // Compute gain reduction when above threshold
            float gain = 1f;
            if (_envelope > thresh && _envelope > 1e-9f)
            {
                float compressed = thresh + (_envelope - thresh) / ratio;
                gain = compressed / _envelope;
            }

            // Apply gain reduction to the sibilance component only
            // Split: sibilance part gets compressed, rest passes through
            float sibilanceComponent = sibilance * gain;
            float residual = input - sibilance;
            buf[i] = residual + sibilanceComponent;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalcFilters()
    {
        // High-Q peaking boost at sibilance frequency to isolate it
        _detectHi.SetPeakingEQ(_frequency, FS, _q, 12f);   // +12 dB boost
        _detectLo.SetPeakingEQ(_frequency * 0.7f, FS, _q * 0.8f, 9f); // +9 dB at lower sibilance
        _dirty = false;
    }

    public void Reset()
    {
        _detectHi.Reset();
        _detectLo.Reset();
        _envelope = 0f;
    }
}
