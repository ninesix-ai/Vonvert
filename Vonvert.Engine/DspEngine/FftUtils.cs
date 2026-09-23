// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/// <summary>
/// FFT utility class — Cooley-Tukey radix-2 forward/inverse FFT.
/// Supports sizes 512, 1024, 2048, 4096. Includes Hann window generation
/// and magnitude spectrum computation. Zero external dependencies.
/// Used by SpectrumAnalyzer, SpectrogramDisplay, and ConvolutionReverb.
/// </summary>
public static class FftUtils
{
    // Pre-computed twiddle factor tables — avoids repeated Cos/Sin per FFT call.
    // Each entry stores (wRe, wIm) arrays for one FFT size.
    private static readonly Dictionary<int, (float[] re, float[] im)> _twiddleCache = new();

    private static (float[] re, float[] im) GetTwiddles(int halfSize)
    {
        if (!_twiddleCache.TryGetValue(halfSize, out var entry))
        {
            var tRe = new float[halfSize];
            var tIm = new float[halfSize];
            for (int j = 0; j < halfSize; j++)
            {
                double angle = -MathF.Tau * j / (halfSize * 2);
                tRe[j] = MathF.Cos((float)angle);
                tIm[j] = MathF.Sin((float)angle);
            }
            entry = (tRe, tIm);
            _twiddleCache[halfSize] = entry;
        }
        return entry;
    }

    /// <summary>Compute in-place radix-2 FFT using pre-computed twiddle factors.
    /// <paramref name="re"/> and <paramref name="im"/> must have the same power-of-two length.</summary>
    public static void FFT(float[] re, float[] im)
    {
        int n = re.Length;
        BitReverse(re, im, n);

        for (int size = 2; size <= n; size <<= 1)
        {
            int half = size >> 1;
            var (twRe, twIm) = GetTwiddles(half);

            for (int start = 0; start < n; start += size)
            {
                for (int j = 0; j < half; j++)
                {
                    float curRe = twRe[j], curIm = twIm[j];
                    int a = start + j;
                    int b = a + half;
                    float tRe = curRe * re[b] - curIm * im[b];
                    float tIm = curRe * im[b] + curIm * re[b];
                    re[b] = re[a] - tRe;
                    im[b] = im[a] - tIm;
                    re[a] += tRe;
                    im[a] += tIm;
                }
            }
        }
    }

    /// <summary>Compute in-place inverse FFT (result is scaled by 1/N).</summary>
    public static void IFFT(float[] re, float[] im)
    {
        int n = re.Length;
        for (int i = 0; i < n; i++) im[i] = -im[i];
        FFT(re, im);
        float inv = 1f / n;
        for (int i = 0; i < n; i++)
        {
            re[i] *= inv;
            im[i] = -im[i] * inv;
        }
    }

    /// <summary>Generate a Hann window of the given length.</summary>
    public static float[] HannWindow(int size)
    {
        var w = new float[size];
        for (int i = 0; i < size; i++)
            w[i] = 0.5f * (1f - MathF.Cos(MathF.Tau * i / (size - 1)));
        return w;
    }

    /// <summary>Compute magnitude spectrum (dB, 0 = full scale) from FFT output.
    /// Returns array of length <paramref name="re"/>.Length / 2 (positive frequencies only).</summary>
    public static float[] MagnitudeDb(float[] re, float[] im)
    {
        int half = re.Length / 2;
        var mag = new float[half];
        for (int i = 0; i < half; i++)
        {
            float m = MathF.Sqrt(re[i] * re[i] + im[i] * im[i]);
            mag[i] = m > 1e-10f ? 20f * MathF.Log10(m) : -100f;
        }
        return mag;
    }

    /// <summary>Check if n is a power of two.</summary>
    public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    private static void BitReverse(float[] re, float[] im, int n)
    {
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
            int m = n >> 1;
            while (m >= 1 && j >= m) { j -= m; m >>= 1; }
            j += m;
        }
    }
}

/// <summary>
/// FFT working buffer class for spectral processing.
/// Manages real/imaginary arrays and provides Complex spectrum access.
/// Used by ConvolutionReverb, Vocoder, and NoiseReduction effects.
/// </summary>
public sealed class FftWork
{
    private readonly int _size;
    private readonly float[] _re;
    private readonly float[] _im;
    private readonly System.Numerics.Complex[] _spectrum;
    private readonly float[] _window;

    public FftWork(int size)
    {
        _size = size;
        _re = new float[size];
        _im = new float[size];
        _spectrum = new System.Numerics.Complex[size];
        _window = FftUtils.HannWindow(size);
    }

    /// <summary>Frequency domain spectrum (Complex array of length FFT size).</summary>
    public System.Numerics.Complex[] Spectrum => _spectrum;

    /// <summary>Perform forward FFT on input time-domain signal.
    /// Applies Hann window, then FFT. Result is in Spectrum.</summary>
    public void Forward(float[] input)
    {
        // Apply window and copy to working buffers
        for (int i = 0; i < _size; i++)
        {
            _re[i] = input[i] * _window[i];
            _im[i] = 0f;
        }

        // Perform FFT
        FftUtils.FFT(_re, _im);

        // Copy to Complex spectrum
        for (int i = 0; i < _size; i++)
            _spectrum[i] = new System.Numerics.Complex(_re[i], _im[i]);
    }

    /// <summary>Perform inverse FFT from Spectrum and write to output.</summary>
    public void Inverse(Span<float> output)
    {
        // Copy from Complex spectrum to working buffers
        for (int i = 0; i < _size; i++)
        {
            _re[i] = (float)_spectrum[i].Real;
            _im[i] = (float)_spectrum[i].Imaginary;
        }

        // Perform IFFT
        FftUtils.IFFT(_re, _im);

        // Copy to output (no synthesis window for overlap-add)
        for (int i = 0; i < _size && i < output.Length; i++)
            output[i] = _re[i];
    }

    /// <summary>Compute magnitude spectrum (linear, not dB) from current Spectrum.</summary>
    public void Magnitudes(Span<float> output)
    {
        int half = _size / 2;
        for (int i = 0; i < half && i < output.Length; i++)
        {
            float re = (float)_spectrum[i].Real;
            float im = (float)_spectrum[i].Imaginary;
            output[i] = MathF.Sqrt(re * re + im * im);
        }
    }
}
