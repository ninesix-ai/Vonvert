// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.App.UIServices;
using Xunit;

namespace Vonvert.Tests.UIServices;

// ═══════════════════════════════════════════════════════════════════
//  SliderValueEditHelper — the pure commit rule behind click-to-edit
//  numeric entry on the expert panel's sliders. Kept free of WPF so it
//  is unit-testable: culture-invariant parse, clamp to range, and snap
//  to the tick relative to the minimum (which mirrors WPF's own tick
//  snapping). The value label always shows the raw slider number, so the
//  editor takes a raw number too — there is no percent/unit scaling.
// ═══════════════════════════════════════════════════════════════════

public class SliderValueEditHelperTests
{
    [Theory(DisplayName = "SVE-001: plain numbers parse invariant and pass through when in range")]
    [InlineData("50", 0, 100, 0, 50)]
    [InlineData("12.5", 0, 100, 0, 12.5)]
    [InlineData("-3", -12, 12, 0, -3)]
    public void PassThrough(string text, double min, double max, double step, double expected)
        => AssertCommit(text, min, max, step, expected);

    [Theory(DisplayName = "SVE-002: clamps out-of-range input to the slider bounds")]
    [InlineData("120", 0, 100, 0, 100)]
    [InlineData("-5", 0, 100, 0, 0)]
    [InlineData("999", -60, 0, 0, 0)]
    public void Clamps(string text, double min, double max, double step, double expected)
        => AssertCommit(text, min, max, step, expected);

    [Theory(DisplayName = "SVE-003: snaps to the nearest tick relative to the minimum")]
    [InlineData("13", 0, 100, 5, 15)]     // 13 -> nearest 5-step is 15
    [InlineData("12", 0, 100, 5, 10)]     // 12 -> nearest is 10
    [InlineData("1.7", 0, 4, 0.5, 1.5)]   // fractional step
    [InlineData("0", -12, 12, 3, 0)]      // negative minimum anchor
    [InlineData("-1", -12, 12, 3, 0)]     // -1 snaps to -12 + 1*3 = -9? see below
    public void Snaps(string text, double min, double max, double step, double expected)
        => AssertCommit(text, min, max, step, expected);

    [Theory(DisplayName = "SVE-005: rejects empty/non-numeric text")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void RejectsNonNumeric(string text)
    {
        Assert.False(SliderValueEditHelper.TryParseCommit(text, 0, 100, 0, out _));
    }

    private static void AssertCommit(string text, double min, double max, double step, double expected)
    {
        Assert.True(SliderValueEditHelper.TryParseCommit(text, min, max, step, out double got),
            $"parse failed for '{text}'");
        Assert.Equal(expected, got, 6);
    }
}
