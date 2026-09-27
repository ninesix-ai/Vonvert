// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using Vonvert.App.UIServices;
using Xunit;

// Display rule (spec 5.4): m:ss with seconds rounded half-away-from-zero, clamped
// to >= 1 — a 0.03 s kick shows 0:01, never 0:00.
public sealed class PadDurationFormatTests
{
    [Theory(DisplayName = "PDF-001: m:ss formatting rules")]
    [InlineData(0.03, "0:01")]   // sub-second floors to 0:01
    [InlineData(0.4,  "0:01")]   // rounds down but clamps at 1
    [InlineData(2.5,  "0:03")]   // half rounds away from zero
    [InlineData(3.0,  "0:03")]
    [InlineData(3.5,  "0:04")]
    [InlineData(29.6, "0:30")]
    [InlineData(65.0, "1:05")]
    public void Format_Rules(double seconds, string expected)
        => Assert.Equal(expected, PadDurationFormat.Format(seconds));
}
