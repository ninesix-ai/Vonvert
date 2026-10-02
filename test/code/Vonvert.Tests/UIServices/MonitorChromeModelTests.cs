// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// CH-001 ~ CH-008: the monitor's chrome state (always-on-top, window size cycle).
// Pure and WPF-free on purpose: the hover toolbar is a thin view over this.
public sealed class MonitorChromeModelTests
{
    [Fact(DisplayName = "CH-001: initial chrome state is topmost + native size, resolved inside a 1080p desktop")]
    public void CH001_InitialNative()
    {
        var c = new MonitorChromeModel();
        Assert.True(c.Topmost);
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);
        Assert.Equal((1280, 720), c.Resolve(1920, 1080));
    }

    [Fact(DisplayName = "CH-002: toggling topmost flips it both ways and never touches the size")]
    public void CH002_TopmostToggle()
    {
        var c = new MonitorChromeModel();
        c.ToggleTopmost();
        Assert.False(c.Topmost);
        Assert.Equal((1280, 720), c.Resolve(1920, 1080));   // size untouched
        c.ToggleTopmost();
        Assert.True(c.Topmost);
    }

    [Fact(DisplayName = "CH-003: the size button cycles Native -> Large -> Fill -> Native")]
    public void CH003_SizeCycleIsDeterministic()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.Large, c.SizeMode);
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.FillWorkArea, c.SizeMode);
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);   // wraps, never a dead end
    }

    [Fact(DisplayName = "CH-004: Large shrinks to the work-area width and keeps 16:9")]
    public void CH004_LargeFitsNarrowDesktop()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();                                        // -> Large (1920x1080 wanted)
        Assert.Equal((1024, 576), c.Resolve(1024, 640));
    }

    [Fact(DisplayName = "CH-005: a short desktop limits the height instead, and never overflows the work area")]
    public void CH005_HeightLimited()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();                                        // Native -> Large (1920x1080 wanted)
        (int w, int h) = c.Resolve(1600, 700);
        Assert.Equal(700, h);                                 // height is the binding limit
        Assert.Equal(1244, w);                                // 700 * 16 / 9, floored
        Assert.True(w <= 1600 && h <= 700, "resolved size must stay inside the work area");
    }

    [Fact(DisplayName = "CH-006: Fill covers the whole work area, not more")]
    public void CH006_FillWorkArea()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();
        c.CycleSize();
        Assert.Equal((2560, 1440), c.Resolve(2560, 1440));
    }

    [Fact(DisplayName = "CH-007: a degenerate work area falls back to the minimum instead of a zero-size window")]
    public void CH007_DegenerateWorkArea()
    {
        Assert.Equal((640, 360), new MonitorChromeModel().Resolve(0, 0));
        Assert.Equal((640, 360), new MonitorChromeModel().Resolve(-1920, -1080));
    }

    [Fact(DisplayName = "CH-008: restoring the default view returns to topmost + native from any state")]
    public void CH008_ResetToDefault()
    {
        var c = new MonitorChromeModel();
        c.ToggleTopmost();
        c.CycleSize();
        c.CycleSize();
        c.ResetToDefault();
        Assert.True(c.Topmost);
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);
    }
}
