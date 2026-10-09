// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.App.UIServices;
using Xunit;

namespace Vonvert.Tests.UIServices;

// ═══════════════════════════════════════════════════════════════════
//  FontScaler — the pure size rule behind global UI font scaling. It is
//  exercised directly (no visual tree, no config) so the small-font boost,
//  the linear scale, and the readability floor are pinned independently of
//  WPF layout.
// ═══════════════════════════════════════════════════════════════════

public class FontScalerTests
{
    // threshold 13.5, boost 0.10, floor 8.0 — the shipped defaults.
    private const double Thr = 13.5, Boost = 0.10, Floor = 8.0;

    [Theory(DisplayName = "FS-001: design sizes at/below the threshold get the small-font boost at 100%")]
    [InlineData(11.0, 12.1)]     // 11 * 1.10
    [InlineData(13.5, 14.85)]    // exactly at threshold → boosted
    [InlineData(9.0, 9.9)]       // 9 * 1.10
    public void SmallFontBoost(double design, double expected)
        => Assert.Equal(expected, FontScaler.ComputeTarget(design, 1.0, Thr, Boost, Floor), 6);

    [Theory(DisplayName = "FS-002: sizes above the threshold are left at their design size at 100%")]
    [InlineData(16.0)]
    [InlineData(20.0)]
    public void LargeFontUntouchedAtDefault(double design)
        => Assert.Equal(design, FontScaler.ComputeTarget(design, 1.0, Thr, Boost, Floor), 6);

    [Theory(DisplayName = "FS-003: the scale multiplies the (possibly boosted) effective size")]
    [InlineData(20.0, 1.4, 28.0)]     // large: 20 * 1.4
    [InlineData(11.0, 1.4, 16.94)]    // small: (11 * 1.10) * 1.4
    [InlineData(11.0, 1.2, 14.52)]    // small: (11 * 1.10) * 1.2
    public void ScaleMultipliesEffective(double design, double scale, double expected)
        => Assert.Equal(expected, FontScaler.ComputeTarget(design, scale, Thr, Boost, Floor), 6);

    [Fact(DisplayName = "FS-004: the floor keeps a boosted tiny size from falling below it")]
    public void FloorBindsTinyText()
    {
        // 5 * 1.10 = 5.5 < floor 8 → 8
        Assert.Equal(8.0, FontScaler.ComputeTarget(5.0, 1.0, Thr, Boost, Floor), 6);
    }

    [Theory(DisplayName = "FS-005: idempotence — a second scale of the same base size is unchanged")]
    [InlineData(11.0)]
    [InlineData(20.0)]
    public void Idempotent(double design)
    {
        double first = FontScaler.ComputeTarget(design, 1.3, Thr, Boost, Floor);
        double again = FontScaler.ComputeTarget(design, 1.3, Thr, Boost, Floor);
        Assert.Equal(first, again, 6);
    }
}
