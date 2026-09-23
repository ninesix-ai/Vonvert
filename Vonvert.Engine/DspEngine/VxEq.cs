// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine;

/*
 * Three-band parametric equaliser — cascaded biquad filters (low shelf,
 * peaking EQ, high shelf). Runs entirely in the time domain so no FFT
 * latency is introduced. Designed for real-time voice shaping.
 */
public sealed class VxEq : IAudioEffect
{
    public string Name      { get; } = "VoiceEQ";
    public bool   IsEnabled { get; set; } = false;

    // Filter stages
    private readonly BiquadFilter _loShelf;
    private readonly BiquadFilter _midBand;
    private readonly BiquadFilter _hiShelf;

    // Backing fields — setter marks _needsUpdate so coefficients are recalculated
    private float _lowGainDb  = 0f;
    private float _lowFreq    = 150f;
    private float _midGainDb  = 0f;
    private float _midFreq    = 4000f;
    private float _midQ       = 1.0f;
    private float _highGainDb = 0f;
    private float _highFreq   = 8000f;

    public float LowGainDb
    {
        get => _lowGainDb;
        set { if (_lowGainDb != value) { _lowGainDb = value; _needsUpdate = true; } }
    }

    public float LowFreq
    {
        get => _lowFreq;
        set { if (_lowFreq != value) { _lowFreq = value; _needsUpdate = true; } }
    }

    public float MidGainDb
    {
        get => _midGainDb;
        set { if (_midGainDb != value) { _midGainDb = value; _needsUpdate = true; } }
    }

    public float MidFreq
    {
        get => _midFreq;
        set { if (_midFreq != value) { _midFreq = value; _needsUpdate = true; } }
    }

    public float MidQ
    {
        get => _midQ;
        set { if (_midQ != value) { _midQ = value; _needsUpdate = true; } }
    }

    public float HighGainDb
    {
        get => _highGainDb;
        set { if (_highGainDb != value) { _highGainDb = value; _needsUpdate = true; } }
    }

    public float HighFreq
    {
        get => _highFreq;
        set { if (_highFreq != value) { _highFreq = value; _needsUpdate = true; } }
    }

    private const float ENGINE_RATE = AudioConstants.EngineRate;
    private volatile bool _needsUpdate = true;

    public VxEq()
    {
        _loShelf   = new BiquadFilter();
        _midBand   = new BiquadFilter();
        _hiShelf   = new BiquadFilter();
    }

    public void Process(Span<float> buffer)
    {
        if (_needsUpdate)
        {
            _loShelf.SetLowShelf(LowFreq, ENGINE_RATE, LowGainDb);
            _midBand.SetPeakingEQ(MidFreq, ENGINE_RATE, MidQ, MidGainDb);
            _hiShelf.SetHighShelf(HighFreq, ENGINE_RATE, HighGainDb);
            _needsUpdate = false;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float sample = buffer[i];
            sample = _loShelf.Process(sample);
            sample = _midBand.Process(sample);
            sample = _hiShelf.Process(sample);
            buffer[i] = sample;
        }
    }

    public void UpdateParams(float lowDb, float midDb, float highDb)
    {
        LowGainDb  = lowDb;
        MidGainDb  = midDb;
        HighGainDb = highDb;
        _needsUpdate = true;
    }

    public void Reset()
    {
        _loShelf.Reset();
        _midBand.Reset();
        _hiShelf.Reset();
    }
}

// --- Biquad filter (direct-form I) ----------------------------------------
internal sealed class BiquadFilter
{
    private float _a0, _a1, _a2, _b0, _b1, _b2;
    private float _s1, _s2; // state registers

    public float Process(float x)
    {
        float y = (x * _b0) + _s1;
        _s1 = (x * _b1) - (y * _a1) + _s2;
        _s2 = (x * _b2) - (y * _a2);
        return y;
    }

    public void Reset() => _s1 = _s2 = 0f;

    public void SetLowShelf(float freq, float fs, float gainDb)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float A  = MathF.Pow(10f, gainDb / 40f);
        float alpha = MathF.Sin(w0) / 2f * MathF.Sqrt((A + 1f / A) * (1f / 1f - 1f) + 2f);

        _b0 =    A*( (A+1f) - (A-1f)*MathF.Cos(w0) + 2f*MathF.Sqrt(A)*alpha );
        _b1 =  2f*A*( (A-1f) - (A+1f)*MathF.Cos(w0)                   );
        _b2 =    A*( (A+1f) - (A-1f)*MathF.Cos(w0) - 2f*MathF.Sqrt(A)*alpha );
        _a0 =        (A+1f) + (A-1f)*MathF.Cos(w0) + 2f*MathF.Sqrt(A)*alpha;
        _a1 = -2f * ( (A-1f) + (A+1f)*MathF.Cos(w0)                   );
        _a2 =        (A+1f) + (A-1f)*MathF.Cos(w0) - 2f*MathF.Sqrt(A)*alpha;

        Normalise();
    }

    public void SetHighShelf(float freq, float fs, float gainDb)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float A  = MathF.Pow(10f, gainDb / 40f);
        float alpha = MathF.Sin(w0) / 2f * MathF.Sqrt((A + 1f / A) * (1f / 1f - 1f) + 2f);

        _b0 =    A*( (A+1f) + (A-1f)*MathF.Cos(w0) + 2f*MathF.Sqrt(A)*alpha );
        _b1 = -2f*A*( (A-1f) + (A+1f)*MathF.Cos(w0)                   );
        _b2 =    A*( (A+1f) + (A-1f)*MathF.Cos(w0) - 2f*MathF.Sqrt(A)*alpha );
        _a0 =        (A+1f) - (A-1f)*MathF.Cos(w0) + 2f*MathF.Sqrt(A)*alpha;
        _a1 =  2f * ( (A-1f) - (A+1f)*MathF.Cos(w0)                   );
        _a2 =        (A+1f) - (A-1f)*MathF.Cos(w0) - 2f*MathF.Sqrt(A)*alpha;

        Normalise();
    }

    public void SetPeakingEQ(float freq, float fs, float Q, float gainDb)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float A  = MathF.Pow(10f, gainDb / 40f);
        float alpha = MathF.Sin(w0) / (2f * Q);

        _b0 =  1f + alpha * A;
        _b1 = -2f * MathF.Cos(w0);
        _b2 =  1f - alpha * A;
        _a0 =  1f + alpha / A;
        _a1 = -2f * MathF.Cos(w0);
        _a2 =  1f - alpha / A;

        Normalise();
    }

    public void SetHighPass(float freq, float fs, float Q = 0.7071f)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float alpha = MathF.Sin(w0) / (2f * Q);
        float cosw0 = MathF.Cos(w0);

        _b0 =  (1f + cosw0) * 0.5f;
        _b1 = -(1f + cosw0);
        _b2 =  (1f + cosw0) * 0.5f;
        _a0 =  1f + alpha;
        _a1 = -2f * cosw0;
        _a2 =  1f - alpha;

        Normalise();
    }

    public void SetLowPass(float freq, float fs, float Q = 0.7071f)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float alpha = MathF.Sin(w0) / (2f * Q);
        float cosw0 = MathF.Cos(w0);

        _b0 =  (1f - cosw0) * 0.5f;
        _b1 =   1f - cosw0;
        _b2 =  (1f - cosw0) * 0.5f;
        _a0 =  1f + alpha;
        _a1 = -2f * cosw0;
        _a2 =  1f - alpha;

        Normalise();
    }

    public void SetBandpass(float freq, float fs, float Q)
    {
        float w0 = 2f * MathF.PI * freq / fs;
        float alpha = MathF.Sin(w0) / (2f * Q);
        float cosw0 = MathF.Cos(w0);

        _b0 =  alpha;
        _b1 =  0f;
        _b2 = -alpha;
        _a0 =  1f + alpha;
        _a1 = -2f * cosw0;
        _a2 =  1f - alpha;

        Normalise();
    }

    private void Normalise()
    {
        _b0 /= _a0; _b1 /= _a0; _b2 /= _a0;
        _a1 /= _a0; _a2 /= _a0; _a0 = 1f;
    }
}
