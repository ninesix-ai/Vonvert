// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using Vonvert.App.UIServices;
using Xunit;

// Tiered duration display (user-verified against real playback): sub-second sounds
// show whole milliseconds, 1-10 s show one decimal, >=10 s show whole seconds.
// The old "0:01 floor" lied about 50-450 ms pads, so it is gone.
public sealed class PadDurationFormatTests
{
    [Theory(DisplayName = "PDF-001: tiered duration formatting rules")]
    [InlineData(0.03,  "30ms")]    // tightest drum click
    [InlineData(0.05,  "50ms")]    // hat
    [InlineData(0.4,   "400ms")]   // snare
    [InlineData(0.999, "999ms")]   // top of ms tier
    [InlineData(1.0,   "1s")]      // whole seconds drop the decimal
    [InlineData(1.2,   "1.2s")]
    [InlineData(3.0,   "3s")]      // rain / campfire
    [InlineData(3.5,   "3.5s")]    // ocean
    [InlineData(9.95,  "10s")]     // decimal carry must bump into the >=10s tier
    [InlineData(29.6,  "30s")]     // imported-file cap
    [InlineData(65.0,  "65s")]
    public void Format_Tiers(double seconds, string expected)
        => Assert.Equal(expected, PadDurationFormat.Format(seconds));
}
