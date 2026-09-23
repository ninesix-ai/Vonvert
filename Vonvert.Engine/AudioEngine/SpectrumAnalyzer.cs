// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.DspEngine;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Real-time spectrum analyser — 1024-point FFT, 32 logarithmic frequency bars.
/// An analysis thread writes audio frames; the UI thread reads bar values via
/// GetSpectrumBars(). Thread-safe through a volatile double-buffered snapshot.
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int FFT_SIZE  = 1024;
    public const int BAR_COUNT = 32;

    private const float SAMPLE_RATE   = 48000f;
    private const float MIN_FREQ      = 20f;
    private const float MAX_FREQ      = 20000f;
    private const float PEAK_DECAY    = 0.97f;

    private readonly float[] _fftRe   = new float[FFT_SIZE];
    private readonly float[] _fftIm   = new float[FFT_SIZE];
    private readonly float[] _window  = FftUtils.HannWindow(FFT_SIZE);
    private readonly float[] _magBuf  = new float[FFT_SIZE / 2];

    private readonly int[]   _barBinMap = new int[BAR_COUNT];
    private readonly float[] _bars      = new float[BAR_COUNT];
    private readonly float[] _peakHold  = new float[BAR_COUNT];

    private int _fftPos;
    // Double-buffered bar snapshot (lock-free ping-pong): the analyzer thread
    // writes the back buffer and publishes it via a volatile swap, so the UI
    // never reads an array being mutated and no per-frame allocation occurs.
    private readonly float[] _dispA = new float[BAR_COUNT];
    private readonly float[] _dispB = new float[BAR_COUNT];
    private float[] _backBuffer;
    private volatile float[] _snapshot;

    public SpectrumAnalyzer()
    {
        BuildBarMap();
        _snapshot = _dispA;
        _backBuffer = _dispB;
    }

    /// <summary>Enable or disable spectrum analysis processing.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Feed audio samples from the analyzer thread.</summary>
    public void FeedSamples(ReadOnlySpan<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            if (_fftPos < FFT_SIZE)
            {
                _fftRe[_fftPos] = samples[i] * _window[_fftPos];
                _fftIm[_fftPos] = 0f;
                _fftPos++;
            }

            if (_fftPos >= FFT_SIZE)
            {
                ProcessFrame();
                _fftPos = 0;
            }
        }
    }

    /// <summary>Get the latest 32 bar values (0–1 normalised). Safe for UI thread.</summary>
    public float[] GetSpectrumBars() => _snapshot;

    /// <summary>Copy current bar values into the supplied span.</summary>
    public void GetSpectrumBars(Span<float> dest)
    {
        var src = _snapshot;
        int len = Math.Min(dest.Length, src.Length);
        src.AsSpan(0, len).CopyTo(dest);
    }

    public void Reset()
    {
        Array.Clear(_fftRe);
        Array.Clear(_fftIm);
        Array.Clear(_magBuf);
        Array.Clear(_bars);
        Array.Clear(_peakHold);
        Array.Clear(_dispA);
        Array.Clear(_dispB);
        _fftPos = 0;
        _snapshot = _dispA;
        _backBuffer = _dispB;
    }

    private void ProcessFrame()
    {
        FftUtils.FFT(_fftRe, _fftIm);

        int half = FFT_SIZE / 2;
        for (int i = 0; i < half; i++)
            _magBuf[i] = MathF.Sqrt(_fftRe[i] * _fftRe[i] + _fftIm[i] * _fftIm[i]);

        for (int b = 0; b < BAR_COUNT; b++)
        {
            int bin = _barBinMap[b];
            int lo = Math.Max(0, bin - 1);
            int hi = Math.Min(half - 1, bin + 1);
            float sum = 0f;
            int count = 0;
            for (int k = lo; k <= hi; k++) { sum += _magBuf[k]; count++; }
            float val = sum / count;

            if (val > _bars[b])
                _bars[b] = val;
            else
                _bars[b] = _bars[b] * 0.7f + val * 0.3f;

            if (_bars[b] > _peakHold[b])
                _peakHold[b] = _bars[b];
            else
                _peakHold[b] *= PEAK_DECAY;
        }

        var back = _backBuffer;
        for (int b = 0; b < BAR_COUNT; b++)
        {
            float db = _bars[b] > 1e-10f ? 20f * MathF.Log10(_bars[b]) : -100f;
            back[b] = Math.Clamp((db + 80f) / 80f, 0f, 1f);
        }
        // Publish the freshly-filled buffer and retire the previous one into
        // the write slot — no allocation.
        _snapshot = back;
        _backBuffer = ReferenceEquals(back, _dispA) ? _dispB : _dispA;
    }

    private void BuildBarMap()
    {
        float logMin = MathF.Log10(MIN_FREQ);
        float logMax = MathF.Log10(MAX_FREQ);
        float binHz  = SAMPLE_RATE / FFT_SIZE;

        for (int b = 0; b < BAR_COUNT; b++)
        {
            float t = (b + 0.5f) / BAR_COUNT;
            float freq = MathF.Pow(10f, logMin + t * (logMax - logMin));
            _barBinMap[b] = Math.Clamp((int)(freq / binHz), 1, FFT_SIZE / 2 - 1);
        }
    }
}
