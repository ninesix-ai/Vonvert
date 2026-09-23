// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * DC Offset removal filter — first-order highpass at ~5 Hz.
 *
 * Removes DC bias (constant offset) from the audio signal that can
 * cause clicks, reduced headroom, or issues with downstream processing.
 * Placed at the beginning of the DSP chain as a safety measure.
 *
 * Algorithm: y[n] = x[n] - x[n-1] + 0.999 * y[n-1]
 * This is a simple one-zero, one-pole highpass with cutoff ≈ 5 Hz.
 *
 * Zero-alloc, stateless apart from the single-pole history.
 */
public sealed class DCOffsetFilter : IAudioEffect
{
    public string Name      { get; } = "DCOffset";
    public bool   IsEnabled { get; set; } = true;  // on by default

    private float _x1;  // previous input
    private float _y1;  // previous output

    private const float COEFF = 0.999f;  // pole → cutoff ≈ 5 Hz @ 48 kHz

    public void Process(Span<float> buf)
    {
        float x1 = _x1;
        float y1 = _y1;

        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];
            float y = x - x1 + COEFF * y1;
            x1 = x;
            y1 = y;
            buf[i] = y;
        }

        _x1 = x1;
        _y1 = y1;
    }

    public void Reset()
    {
        _x1 = 0f;
        _y1 = 0f;
    }
}
