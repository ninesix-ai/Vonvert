// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// LS-001 ~ LS-003: dB→meter mapping is clamped, monotonic and endpoint-exact.
public sealed class LufsScaleTests
{
    [Fact(DisplayName = "LS-001: endpoints map to 0 (top, 0 dB) and 1 (bottom, -60 LUFS)")]
    public void LS001_Endpoints()
    {
        Assert.Equal(0.0, LufsScale.ToRatio(0f), 3);
        Assert.Equal(1.0, LufsScale.ToRatio(-60f), 3);
    }

    [Fact(DisplayName = "LS-002: values outside [-60, 0] clamp instead of overshooting")]
    public void LS002_Clamp()
    {
        Assert.Equal(0.0, LufsScale.ToRatio(6f), 3);
        Assert.Equal(1.0, LufsScale.ToRatio(-120f), 3);
    }

    [Fact(DisplayName = "LS-003: louder means higher on screen (ratio decreases monotonically)")]
    public void LS003_Monotonic()
    {
        Assert.True(LufsScale.ToRatio(-10f) < LufsScale.ToRatio(-30f));
        Assert.Equal(50.0, LufsScale.MapToPixel(-30f, 100.0), 3);   // mid of a 100px meter
    }
}
