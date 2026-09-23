// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using NAudio.Wave;

namespace Vonvert.Engine.AudioEngine;

/*
 * Converts arbitrary device audio (any rate, channel count, bit depth)
 * into 48 kHz mono IEEE float32.
 *
 * All internal buffers are pre-allocated and reused across callbacks so
 * that the hot path never triggers a garbage collection.  When the
 * incoming data exceeds the current buffer size the arrays are grown
 * in-place (replaced) and never shrunk.
 *
 * Resampling uses 4-point Catmull-Rom (cubic Hermite) interpolation
 * which provides significantly better frequency response than linear
 * while remaining efficient enough for real-time processing.
 */
public sealed class FormatConverter
{
    // Derive from AudioConstants so the conversion target always matches the
    // engine's DSP rate.
    private const int TARGET_HZ = AudioConstants.EngineRate;

    // Pre-allocated working buffers — grow on demand, never shrink.
    private float[] _pcm    = new float[8192];
    private float[] _mono   = new float[8192];
    private float[] _upshot = new float[8192];

    /// <summary>
    /// Decode, downmix, and resample a raw device buffer to 48 kHz mono float.
    /// </summary>
    public Span<float> Convert(byte[] raw, int count, WaveFormat fmt)
    {
        int pcmLen = DecodePcm(raw, count, fmt);
        Span<float> src = _pcm.AsSpan(0, pcmLen);

        if (fmt.Channels > 1)
        {
            int monoLen = MixDown(src, fmt.Channels);
            src = _mono.AsSpan(0, monoLen);
        }

        if (fmt.SampleRate != TARGET_HZ)
        {
            int outLen = Interpolate(src, fmt.SampleRate, TARGET_HZ);
            return _upshot.AsSpan(0, outLen);
        }

        return src;
    }

    // ── PCM decode ───────────────────────────────────────────────────────

    private int DecodePcm(byte[] raw, int count, WaveFormat fmt)
    {
        int bytesPerSample = fmt.BitsPerSample / 8;
        int totalSamples   = count / bytesPerSample;
        if (_pcm.Length < totalSamples) _pcm = new float[totalSamples];

        switch (fmt.BitsPerSample)
        {
            case 32 when fmt.Encoding == WaveFormatEncoding.IeeeFloat:
                System.Buffer.BlockCopy(raw, 0, _pcm, 0, count);
                break;

            case 16:
                for (int i = 0; i < totalSamples; i++)
                    _pcm[i] = BitConverter.ToInt16(raw, i * 2) / 32768f;
                break;

            case 24:
                for (int i = 0; i < totalSamples; i++)
                {
                    int v = raw[i * 3] | (raw[i * 3 + 1] << 8) | ((sbyte)raw[i * 3 + 2] << 16);
                    _pcm[i] = v / 8388608f;
                }
                break;

            case 32:
                for (int i = 0; i < totalSamples; i++)
                    _pcm[i] = BitConverter.ToInt32(raw, i * 4) / 2147483648f;
                break;

            default:
                for (int i = 0; i < totalSamples; i++)
                    _pcm[i] = raw[i] / 128f - 1f;
                break;
        }
        return totalSamples;
    }

    // ── Stereo / multi → mono ────────────────────────────────────────────

    private int MixDown(ReadOnlySpan<float> src, int channels)
    {
        int frames = src.Length / channels;
        if (_mono.Length < frames) _mono = new float[frames];
        float scale = 1f / channels;

        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += src[f * channels + c];
            _mono[f] = sum * scale;
        }
        return frames;
    }

    // ── Catmull-Rom resampler ────────────────────────────────────────────

    private int Interpolate(ReadOnlySpan<float> src, int srcHz, int dstHz)
    {
        double ratio = (double)dstHz / srcHz;
        int outLen   = (int)(src.Length * ratio);
        if (_upshot.Length < outLen) _upshot = new float[outLen];
        int srcLen = src.Length;

        for (int i = 0; i < outLen; i++)
        {
            double pos = i / ratio;
            int lo     = (int)pos;
            float frac = (float)(pos - lo);

            // Four neighbouring samples for cubic interpolation
            int xm1 = Math.Max(lo - 1, 0);
            int x0  = lo;
            int x1  = Math.Min(lo + 1, srcLen - 1);
            int x2  = Math.Min(lo + 2, srcLen - 1);

            float ym1 = src[xm1], y0 = src[x0], y1 = src[x1], y2 = src[x2];

            // Catmull-Rom basis
            float t  = frac;
            float t2 = t * t;
            float t3 = t2 * t;
            _upshot[i] = 0.5f * (
                2f * y0 +
                (-ym1 + y1) * t +
                (2f * ym1 - 5f * y0 + 4f * y1 - y2) * t2 +
                (-ym1 + 3f * y0 - 3f * y1 + y2) * t3);
        }
        return outLen;
    }
}
