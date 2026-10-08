// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// The FFT twiddle cache is shared static state: FftUtils.FFT runs on the analyzer
// worker thread, the audio thread, and in parallel tests all at once. This guards
// that concurrent calls stay correct and throw nothing — a regression net against
// the cache ever reverting to a non-thread-safe form (the root cause of the flaky
// RT-006 / PW-003).

namespace Vonvert.Tests.DSP;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vonvert.Engine.DspEngine;
using Xunit;

public sealed class FftUtilsConcurrencyTests
{
    private static float[] Sine(int n)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = 0.7f * MathF.Sin(2f * MathF.PI * 1000f * i / 48000f);
        return s;
    }

    [Fact(DisplayName = "FFT-CONC-001: concurrent FFTs across sizes match the single-threaded result and throw nothing")]
    public void ConcurrentFftAcrossSizes_StaysCorrect()
    {
        int[] sizes = { 512, 1024, 2048, 4096 };

        // Reference computed once, single-threaded.
        var reference = new Dictionary<int, (float[] re, float[] im)>();
        foreach (var n in sizes)
        {
            var re = Sine(n); var im = new float[n];
            FftUtils.FFT(re, im);
            reference[n] = (re, im);
        }

        // Hammer the same sizes from many threads and require every result to match.
        var ex = Record.Exception(() => Parallel.For(0, 400, i =>
        {
            var n = sizes[i % sizes.Length];
            var re = Sine(n); var im = new float[n];
            FftUtils.FFT(re, im);
            var (rre, rim) = reference[n];
            for (int k = 0; k < n; k++)
            {
                Assert.True(MathF.Abs(re[k] - rre[k]) < 1e-3f, $"re[{k}] diverged (size {n}, iter {i})");
                Assert.True(MathF.Abs(im[k] - rim[k]) < 1e-3f, $"im[{k}] diverged (size {n}, iter {i})");
            }
        }));

        Assert.Null(ex);
    }
}
