// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine;

/*
 * 10-band graphic equaliser with voice-optimised centre frequencies.
 * Each band is a BiquadFilter: band 0 = low shelf, bands 1-8 = peaking EQ
 * (Q=1.4 ≈ 1 octave), band 9 = high shelf. Gains range ±12 dB.
 */
public sealed class GraphicEQEffect : IAudioEffect
{
    public string Name      { get; } = "GraphicEQ";
    public bool   IsEnabled { get; set; } = false;

    /// <summary>10 voice-optimised centre frequencies (Hz).</summary>
    public static readonly float[] Frequencies =
        { 80f, 160f, 400f, 800f, 1600f, 2400f, 3200f, 4800f, 7000f, 12000f };

    private const int BANDS = 10;
    private const float ENGINE_RATE = AudioConstants.EngineRate;
    private const float DEFAULT_Q   = 1.4f;

    /// <summary>Per-band gain in dB (±12). Index matches <see cref="Frequencies"/>.</summary>
    public float[] GainsDb { get; } = new float[BANDS];

    private readonly BiquadFilter[] _bands = new BiquadFilter[BANDS];
    private volatile bool _needsUpdate = true;

    public GraphicEQEffect()
    {
        for (int i = 0; i < BANDS; i++)
            _bands[i] = new BiquadFilter();
    }

    /// <summary>Set gain for a single band (0-9). Marks coefficients dirty.</summary>
    public void SetBandGain(int band, float gainDb)
    {
        if (band < 0 || band >= BANDS) return;
        if (GainsDb[band] != gainDb)
        {
            GainsDb[band] = gainDb;
            _needsUpdate = true;
        }
    }

    public void Process(Span<float> buffer)
    {
        if (_needsUpdate)
        {
            UpdateCoefficients();
            _needsUpdate = false;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float sample = buffer[i];
            for (int b = 0; b < BANDS; b++)
                sample = _bands[b].Process(sample);
            buffer[i] = sample;
        }
    }

    public void Reset()
    {
        for (int i = 0; i < BANDS; i++)
            _bands[i].Reset();
    }

    private void UpdateCoefficients()
    {
        // Band 0: Low Shelf
        _bands[0].SetLowShelf(Frequencies[0], ENGINE_RATE, GainsDb[0]);

        // Bands 1-8: Peaking EQ
        for (int b = 1; b < BANDS - 1; b++)
            _bands[b].SetPeakingEQ(Frequencies[b], ENGINE_RATE, DEFAULT_Q, GainsDb[b]);

        // Band 9: High Shelf
        _bands[BANDS - 1].SetHighShelf(Frequencies[BANDS - 1], ENGINE_RATE, GainsDb[BANDS - 1]);
    }
}
