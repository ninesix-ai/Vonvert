// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Numerics;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.Diagnostics;

/*
 * Spectrogram Display Service — real-time 3D waterfall visualization data.
 *
 * Generates time-frequency-amplitude data for a spectrogram/waterfall
 * display. Each FFT frame produces one column of frequency magnitude
 * values, stored in a circular history buffer.
 *
 * Features:
 *   - Configurable FFT size (512/1024/2048/4096, powers of two)
 *   - Quadratic (log-like) frequency axis with configurable bin count
 *   - Circular history buffer (last N frames)
 *   - Hann windowing for reduced spectral leakage
 *   - Peak hold for each frequency bin
 *
 * Single-writer: feed from the analyzer thread; UI readers may observe a
 * column mid-update — values are independent magnitudes, so the worst case
 * for a display consumer is a one-frame visual seam, never structural tearing.
 */
public sealed class SpectrogramDisplay
{
    private const int SAMPLE_RATE = AudioConstants.EngineRate;

    // ── Configuration ─────────────────────────────────────────────────────

    private readonly int _fftSize;
    private readonly int _halfSize;
    private readonly int _historyDepth;
    private readonly int _frequencyBins;

    // ── FFT working area ──────────────────────────────────────────────────

    private readonly float[] _window;
    private readonly float[] _inputFrame;
    private readonly Complex[] _spectrum;
    private readonly int[] _perm;

    // Input ring buffer
    private readonly float[] _ringBuffer;
    private int _ringWrite;
    private int _ringFill;

    // ── Output data ───────────────────────────────────────────────────────

    // History buffer: [frequencyBin, timeFrame]
    private readonly float[,] _history;
    private int _historyWrite;
    private int _historyCount;

    // Peak hold buffer
    private readonly float[] _peakHold;
    private float _peakDecayRate;

    // Current column (most recent FFT frame)
    private readonly float[] _currentColumn;

    // ── Accumulation ──────────────────────────────────────────────────────

    private int _samplesSinceLastFft;
    private readonly int _hopSize;

    // ═══════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a spectrogram display service.
    /// </summary>
    /// <param name="fftSize">FFT size (512, 1024, 2048, 4096). Higher = better frequency resolution.</param>
    /// <param name="frequencyBins">Number of frequency bins in output (typical: 64-256).</param>
    /// <param name="historyDepth">Number of time frames to keep (typical: 100-500).</param>
    public SpectrogramDisplay(int fftSize = 2048, int frequencyBins = 128, int historyDepth = 200)
    {
        // The in-place radix-2 FFT and the ring/history index arithmetic below
        // are only valid for power-of-two sizes; reject misuse at construction
        // instead of silently producing wrong spectra.
        if (fftSize < 512 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException($"fftSize must be a power of two >= 512, got {fftSize}", nameof(fftSize));
        if (frequencyBins <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequencyBins));
        if (historyDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(historyDepth));
        _fftSize = fftSize;
        _halfSize = fftSize / 2 + 1;
        _hopSize = fftSize / 2;
        _frequencyBins = frequencyBins;
        _historyDepth = historyDepth;

        // Pre-allocate FFT resources
        _window = BuildHann(fftSize);
        _inputFrame = new float[fftSize];
        _spectrum = new Complex[fftSize];
        _perm = BuildPermutation(fftSize);

        // Ring buffer: 2× FFT for safe wrap
        _ringBuffer = new float[fftSize * 2];

        // History buffer
        _history = new float[frequencyBins, historyDepth];
        _currentColumn = new float[frequencyBins];
        _peakHold = new float[frequencyBins];

        // Peak decay: ~2 seconds to fall 60 dB
        _peakDecayRate = 60f / (2f * SAMPLE_RATE / _hopSize);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Feed samples
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Feed audio samples. When enough accumulate, compute one FFT frame.</summary>
    public void FeedSamples(ReadOnlySpan<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            // Keep the write cursor inside the ring: an unbounded counter would
            // overflow after ~12.4 h at 48 kHz and produce negative indices.
            _ringBuffer[_ringWrite] = samples[i];
            _ringWrite = (_ringWrite + 1) % _ringBuffer.Length;
            _ringFill++;
            _samplesSinceLastFft++;

            if (_ringFill >= _fftSize && _samplesSinceLastFft >= _hopSize)
            {
                ProcessFrame();
                _samplesSinceLastFft = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FFT processing
    // ═══════════════════════════════════════════════════════════════════════

    private void ProcessFrame()
    {
        // Copy windowed frame from ring buffer
        int readPos = (_ringWrite - _ringFill + _ringBuffer.Length) % _ringBuffer.Length;
        for (int i = 0; i < _fftSize; i++)
            _inputFrame[i] = _ringBuffer[(readPos + i) % _ringBuffer.Length] * _window[i];
        _ringFill -= _hopSize;

        // Forward FFT
        for (int i = 0; i < _fftSize; i++)
            _spectrum[i] = new Complex(_inputFrame[i], 0);
        ForwardInPlace();

        // Compute magnitudes and map to frequency bins
        for (int b = 0; b < _frequencyBins; b++)
        {
            int fftBin = FftBinFor(b);

            float magnitude = (float)_spectrum[fftBin].Magnitude;
            float db = magnitude > 1e-10f ? 20f * MathF.Log10(magnitude) : -100f;

            // Normalize to 0-1 range (-100 to 0 dB → 0 to 1)
            _currentColumn[b] = Math.Clamp((db + 100f) / 100f, 0f, 1f);

            // Peak hold with decay
            if (_currentColumn[b] > _peakHold[b])
                _peakHold[b] = _currentColumn[b];
            else
                _peakHold[b] = MathF.Max(0f, _peakHold[b] - _peakDecayRate / 100f);
        }

        // Store in history
        int writeCol = _historyWrite % _historyDepth;
        for (int b = 0; b < _frequencyBins; b++)
            _history[b, writeCol] = _currentColumn[b];

        _historyWrite++;
        _historyCount = Math.Min(_historyCount + 1, _historyDepth);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Data access (UI thread)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Get the current spectrogram column (most recent FFT frame).</summary>
    public ReadOnlySpan<float> GetCurrentColumn() => _currentColumn;

    /// <summary>Get peak hold values for each frequency bin.</summary>
    public ReadOnlySpan<float> GetPeakHold() => _peakHold;

    /// <summary>Map an output bin index to its FFT bin (quadratic, log-like spacing).
    /// Single definition shared by ProcessFrame and GetBinFrequencyHz so the
    /// rendered energy and the UI frequency labels can never drift apart.</summary>
    private int FftBinFor(int bin)
    {
        float freqNorm = (float)bin / _frequencyBins;
        return Math.Clamp((int)(freqNorm * freqNorm * (_halfSize - 1)), 0, _halfSize - 1);
    }

    /// <summary>Get the frequency in Hz for a given bin index.</summary>
    public float GetBinFrequencyHz(int binIndex)
        => FftBinFor(binIndex) * (float)SAMPLE_RATE / _fftSize;

    /// <summary>Get history data as a flat array [frequencyBin × timeFrame].</summary>
    /// <returns>Tuple of (data, frequencyBins, timeFrames, writePosition)</returns>
    public (float[,] data, int freqBins, int timeFrames, int writePos) GetHistory()
        => (_history, _frequencyBins, _historyCount, _historyWrite % _historyDepth);

    /// <summary>Number of frequency bins.</summary>
    public int FrequencyBins => _frequencyBins;

    /// <summary>Number of time frames in history.</summary>
    public int TimeFrames => _historyCount;

    /// <summary>FFT size used for analysis.</summary>
    public int FftSize => _fftSize;

    // ═══════════════════════════════════════════════════════════════════════
    // Reset
    // ═══════════════════════════════════════════════════════════════════════

    public void Reset()
    {
        Array.Clear(_history);
        Array.Clear(_peakHold);
        Array.Clear(_currentColumn);
        Array.Clear(_ringBuffer);
        _ringWrite = 0;
        _ringFill = 0;
        _historyWrite = 0;
        _historyCount = 0;
        _samplesSinceLastFft = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FFT implementation (radix-2, in-place)
    // ═══════════════════════════════════════════════════════════════════════

    private void ForwardInPlace()
    {
        // Bit-reversal permutation
        for (int i = 0; i < _fftSize; i++)
        {
            int j = _perm[i];
            if (j > i)
                (_spectrum[i], _spectrum[j]) = (_spectrum[j], _spectrum[i]);
        }

        // Cooley-Tukey butterflies
        for (int len = 2; len <= _fftSize; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            var wn = new Complex(Math.Cos(angle), Math.Sin(angle));
            int half = len >> 1;
            for (int i = 0; i < _fftSize; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < half; k++)
                {
                    var t = w * _spectrum[i + k + half];
                    var u = _spectrum[i + k];
                    _spectrum[i + k] = u + t;
                    _spectrum[i + k + half] = u - t;
                    w *= wn;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Static helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static float[] BuildHann(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / n));
        return w;
    }

    private static int[] BuildPermutation(int n)
    {
        var p = new int[n];
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            p[i] = j;
            int m = n >> 1;
            while (m >= 1 && j >= m) { j -= m; m >>= 1; }
            j += m;
        }
        return p;
    }
}
