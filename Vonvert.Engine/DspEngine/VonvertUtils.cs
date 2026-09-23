// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Runtime.CompilerServices;

namespace Vonvert.Engine.DspEngine;

/*
 * Miscellaneous DSP helper functions used across the Vonvert audio graph.
 * Provides fast math approximations and dB ? linear conversions optimised
 * for the real-time processing path.
 */
public static class VonvertUtils
{
    /// <summary>Convert decibels to linear gain. 0 dB → 1.0, -6 dB → ~0.5.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);

    /// <summary>Convert linear gain to decibels. Guards against log(0).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToDb(float linear)
    {
        if (linear < 1e-10f) return -200f;
        return 20f * MathF.Log10(linear);
    }

    /// <summary>Fast soft-clip via tanh approximation (branchless, ~3× faster than MathF.Tanh).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SoftClip(float x)
    {
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }

    /// <summary>Linear interpolation between a and b by factor t ∈ [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Peak meter — returns the maximum absolute value in the span.</summary>
    public static float PeakLevel(ReadOnlySpan<float> buffer)
    {
        float peak = 0f;
        foreach (var s in buffer)
        {
            float abs = MathF.Abs(s);
            if (abs > peak) peak = abs;
        }
        return peak;
    }

    /// <summary>RMS energy of a buffer.</summary>
    public static float RmsEnergy(ReadOnlySpan<float> buffer)
    {
        float sum = 0f;
        foreach (var s in buffer)
            sum += s * s;
        return MathF.Sqrt(sum / buffer.Length);
    }
}
