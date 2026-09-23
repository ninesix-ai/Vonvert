// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Bitcrusher — reduces bit depth and sample rate for lo-fi / retro voice.
 *
 * Features:
 *   - BitDepth: amplitude quantisation (1 = extreme, 24 = transparent).
 *   - SampleRateReduction: sample-and-hold decimation (1 – 20×).
 *   - Dither: triangular (TPDF) dither before quantisation to reduce
 *     correlated quantisation distortion.
 *   - QuantCurve: Linear (uniform steps), Logarithmic (perceptual),
 *     or Sine (soft-knee, musical character).
 *
 * Zero-alloc on DSP thread.
 */
public sealed class BitcrusherEffect : IAudioEffect
{
    public string Name      { get; } = "Bitcrusher";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _bitDepth = 8f;
    /// <summary>Effective bit depth: 1 (heaviest crush) to 24 (transparent). Default: 8.</summary>
    public float BitDepth
    {
        get => _bitDepth;
        set => _bitDepth = Math.Clamp(value, 1f, 24f);
    }

    private int _sampleReduction = 1;
    /// <summary>Sample-rate reduction factor: 1 (none) to 20 (heavy). Default: 1.</summary>
    public int SampleRateReduction
    {
        get => _sampleReduction;
        set => _sampleReduction = (int)Math.Clamp(value, 1, 20);
    }

    /// <summary>Quantisation curve shape.</summary>
    public enum QuantCurve { Linear, Logarithmic, Sine }

    private QuantCurve _curve = QuantCurve.Linear;
    /// <summary>Quantisation curve. Default: Linear.</summary>
    public QuantCurve Curve
    {
        get => _curve;
        set => _curve = value;
    }

    private bool _dither = false;
    /// <summary>Enable TPDF dither before quantisation. Default: false.</summary>
    public bool Dither
    {
        get => _dither;
        set => _dither = value;
    }

    // ── Internal state ────────────────────────────────────────────────────

    private int   _holdCounter = 0;
    private float _heldSample  = 0f;

    public void Process(Span<float> buf)
    {
        float levels = MathF.Pow(2f, _bitDepth);
        float inv    = 1f / levels;
        int   hold   = _sampleReduction;
        bool  dith   = _dither;
        var   curve  = _curve;

        for (int i = 0; i < buf.Length; i++)
        {
            if (_holdCounter <= 0)
            {
                float x = buf[i];

                // Apply dither before quantisation
                if (dith)
                {
                    // TPDF dither: sum of two uniform random values in [-1, 1]
                    float r = Random.Shared.NextSingle() + Random.Shared.NextSingle() - 1f;
                    x += r * inv;  // dither amplitude = 1 LSB
                }

                // Quantise according to curve
                _heldSample = Quantise(x, levels, inv, curve);
                _holdCounter = hold;
            }

            buf[i] = _heldSample;
            _holdCounter--;
        }
    }

    private static float Quantise(float x, float levels, float inv, QuantCurve curve)
    {
        switch (curve)
        {
            case QuantCurve.Logarithmic:
            {
                // Perceptual quantisation: compress before, expand after
                float sign = x >= 0f ? 1f : -1f;
                float compressed = MathF.Log(1f + MathF.Abs(x) * 4f) / MathF.Log(5f);
                float quantised = MathF.Round(compressed * levels) * inv;
                return sign * (MathF.Exp(quantised * MathF.Log(5f)) - 1f) * 0.25f;
            }
            case QuantCurve.Sine:
            {
                // Soft-knee: maps through sin for musical character
                float mapped = MathF.Sin(x * MathF.PI * 0.5f);  // soft clip to [-1,1]
                float quantised = MathF.Round(mapped * levels) * inv;
                return MathF.Asin(Math.Clamp(quantised, -1f, 1f)) / (MathF.PI * 0.5f);
            }
            default: // Linear
                return MathF.Round(x * levels) * inv;
        }
    }

    public void Reset()
    {
        _holdCounter = 0;
        _heldSample  = 0f;
    }
}
