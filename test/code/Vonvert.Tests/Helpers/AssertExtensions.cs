// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Custom assertion helpers for span/float comparisons in audio tests.

using Xunit;

namespace Vonvert.Tests.Helpers;

public static class AssertExtensions
{
    public static void SpanApproxEqual(ReadOnlySpan<float> expected, ReadOnlySpan<float> actual, float eps = 1e-5f, string because = "")
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            float diff = MathF.Abs(expected[i] - actual[i]);
            if (diff > eps)
                Assert.Fail($"Span mismatch at index {i}: expected {expected[i]:G6}, actual {actual[i]:G6}, diff={diff:G3} > eps={eps:G3}. {because}");
        }
    }

    public static void SpanEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual)
        where T : IEquatable<T>
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], actual[i]);
    }

    public static void SpanEqual(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        SpanEqual<float>(expected.AsSpan(), actual.AsSpan());
    }

    public static void AllInRange(ReadOnlySpan<float> buf, float min = -1f, float max = 1f)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            float s = buf[i];
            if (!float.IsFinite(s) || s < min || s > max)
                Assert.Fail($"Sample [{i}] = {s:G5} out of [{min},{max}] or non-finite");
        }
    }

    public static void NotSilent(ReadOnlySpan<float> buf, float threshold = 0.001f)
    {
        for (int i = 0; i < buf.Length; i++)
            if (MathF.Abs(buf[i]) > threshold) return;
        Assert.Fail("Buffer is fully silent but expected non-silent output");
    }

    public static void AllSilent(ReadOnlySpan<float> buf, float threshold = 1e-5f)
    {
        for (int i = 0; i < buf.Length; i++)
            if (MathF.Abs(buf[i]) > threshold)
                Assert.Fail($"Sample [{i}] = {buf[i]:G5} exceeds silence threshold {threshold}");
    }

    public static void RmsReductionApprox(double before, double after, double expectedRatio, double tolerance = 0.2)
    {
        double ratio = after / Math.Max(before, 1e-9);
        double diff = Math.Abs(ratio - expectedRatio);
        Assert.True(diff < tolerance,
            $"Expected RMS ratio ≈ {expectedRatio:F3}, actual {ratio:F3} (before={before:G4}, after={after:G4}, diff={diff:G3})");
    }
}