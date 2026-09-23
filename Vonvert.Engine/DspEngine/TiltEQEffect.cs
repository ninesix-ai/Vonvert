// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Tilt EQ — single-knob equaliser that tilts the frequency balance
 * between bass and treble around a fixed pivot frequency.
 *
 *   Tilt > 0 → brighter (boost highs, cut lows)
 *   Tilt < 0 → warmer (boost lows, cut highs)
 *   Tilt = 0 → flat
 *
 * Implementation: a single shelving biquad that smoothly transitions
 * between low-shelf boost and high-shelf boost around the pivot.
 * Uses the existing BiquadFilter class.
 *
 * Zero-alloc on DSP thread.
 */
public sealed class TiltEQEffect : IAudioEffect
{
    public string Name      { get; } = "TiltEQ";
    public bool   IsEnabled { get; set; }

    private float _tilt = 0f;  // -1 (warm) to +1 (bright)

    /// <summary>Tilt amount: -1 (maximum warmth) to +1 (maximum brightness). Default: 0.</summary>
    public float Tilt
    {
        get => _tilt;
        set { _tilt = Math.Clamp(value, -1f, 1f); _dirty = true; }
    }

    /// <summary>Maximum tilt gain in dB (applied at extremes). Default: 6.</summary>
    public float MaxGainDb { get; set; } = 6f;

    /// <summary>Pivot frequency where the tilt crosses 0 dB. Default: 1000 Hz.</summary>
    public float PivotFreq { get; set; } = 1000f;

    // ── Internal state ────────────────────────────────────────────────────

    private const float FS = 48000f;
    private readonly BiquadFilter _filter = new();
    private volatile bool _dirty = true;

    public void Process(Span<float> buf)
    {
        if (_dirty) RecalcFilter();

        for (int i = 0; i < buf.Length; i++)
            buf[i] = _filter.Process(buf[i]);
    }

    private void RecalcFilter()
    {
        // Tilt maps to a shelf gain: positive tilt → high shelf boost,
        // negative tilt → low shelf cut (equivalent to high shelf boost
        // with inverted polarity around the pivot).
        float gainDb = _tilt * MaxGainDb;

        if (MathF.Abs(gainDb) < 0.01f)
        {
            // Flat: unity peaking (effectively bypass)
            _filter.SetPeakingEQ(PivotFreq, FS, 0.7071f, 0f);
        }
        else if (gainDb > 0f)
        {
            // Bright: high shelf boost
            _filter.SetHighShelf(PivotFreq, FS, gainDb);
        }
        else
        {
            // Warm: low shelf boost (negative high shelf = low boost)
            _filter.SetLowShelf(PivotFreq, FS, -gainDb);
        }

        _dirty = false;
    }

    public void Reset() => _filter.Reset();
}
