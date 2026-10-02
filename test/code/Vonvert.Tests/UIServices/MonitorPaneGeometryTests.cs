// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// GM-001 ~ GM-008: proportional pane geometry for the fullscreen monitor.
//
// The waveform row and the loudness column used to be 140 px and 150 px no matter how
// big the window was. Captured small into a corner - the most common streamer layout -
// that left the main view 490x220 at the minimum size: the picture everyone opens the
// monitor for was the one thing that got squeezed away. These rules scale the strips
// with the window while keeping the shipped 1280x720 look byte-for-byte identical.
public sealed class MonitorPaneGeometryTests
{
    [Fact(DisplayName = "GM-001: the shipped 1280x720 defaults are unchanged (140 / 150)")]
    public void GM001_DefaultLookIsPreserved()
    {
        var g = MonitorPaneGeometry.Compute(1280, 720);
        Assert.Equal(140, g.WaveformRowHeight);
        Assert.Equal(150, g.LoudnessColumnWidth);
        Assert.Equal(1130, g.MainWidth);
        Assert.Equal(580, g.MainHeight);
    }

    [Fact(DisplayName = "GM-002: at 1080p the strips grow with the window, keeping the main view's share")]
    public void GM002_ScalesUp()
    {
        var small = MonitorPaneGeometry.Compute(1280, 720);
        var big = MonitorPaneGeometry.Compute(1920, 1080);
        Assert.True(big.WaveformRowHeight > small.WaveformRowHeight);
        Assert.True(big.LoudnessColumnWidth > small.LoudnessColumnWidth);
        // The main view must keep roughly the same share of the window, not a smaller one.
        Assert.Equal((double)small.MainWidth / 1280, (double)big.MainWidth / 1920, 2);
        Assert.Equal((double)small.MainHeight / 720, (double)big.MainHeight / 1080, 2);
    }

    [Fact(DisplayName = "GM-003: at the minimum size the main view still dominates instead of collapsing")]
    public void GM003_MinimumSizeStaysReadable()
    {
        var g = MonitorPaneGeometry.Compute(640, 360);
        Assert.True(g.MainHeight >= 360 * 0.60,
            $"main view is only {g.MainHeight}px of 360 - the picture must keep most of the window");
        Assert.True(g.MainWidth >= 640 * 0.70,
            $"main view is only {g.MainWidth}px of 640");
    }

    [Fact(DisplayName = "GM-004: heights stay inside the floor and ceiling so strips never vanish or hog the window")]
    public void GM004_Clamps()
    {
        Assert.Equal(MonitorPaneGeometry.MinWaveformHeight, MonitorPaneGeometry.WaveformHeight(200));
        Assert.Equal(MonitorPaneGeometry.MaxWaveformHeight, MonitorPaneGeometry.WaveformHeight(5000));
        Assert.Equal(MonitorPaneGeometry.MinLoudnessWidth, MonitorPaneGeometry.LoudnessWidth(400));
        Assert.Equal(MonitorPaneGeometry.MaxLoudnessWidth, MonitorPaneGeometry.LoudnessWidth(5000));
    }

    [Theory(DisplayName = "GM-005: bigger window, never a smaller strip")]
    [InlineData(400, 200)]
    [InlineData(900, 400)]
    [InlineData(1600, 900)]
    [InlineData(3840, 2160)]
    public void GM005_Monotonic(int width, int height)
    {
        var a = MonitorPaneGeometry.Compute(width, height);
        var b = MonitorPaneGeometry.Compute(width * 2, height * 2);
        Assert.True(b.WaveformRowHeight >= a.WaveformRowHeight);
        Assert.True(b.LoudnessColumnWidth >= a.LoudnessColumnWidth);
    }

    [Fact(DisplayName = "GM-006: a portrait canvas gets usable strips instead of a squashed main view")]
    public void GM006_Portrait()
    {
        var g = MonitorPaneGeometry.Compute(1080, 1920);     // 9:16 vertical canvas (Shorts, Reels)
        Assert.True(g.MainWidth > 1080 * 0.60);
        Assert.True(g.MainHeight > 1920 * 0.60);
    }

    [Fact(DisplayName = "GM-007: nonsense sizes fall back to the floor rather than producing zero or negative geometry")]
    public void GM007_DegenerateInput()
    {
        var g = MonitorPaneGeometry.Compute(0, 0);
        Assert.True(g.MainWidth > 0 && g.MainHeight > 0, $"degenerate input gave {g.MainWidth}x{g.MainHeight}");
        Assert.Equal(g.MainWidth, MonitorPaneGeometry.Compute(-500, -500).MainWidth);
    }

    [Fact(DisplayName = "GM-008: the guidance banner narrows with the window so it cannot cover the corner controls")]
    public void GM008_GuidanceWidth()
    {
        Assert.Equal(620, MonitorPaneGeometry.GuidanceMaxWidth(1920));     // ceiling
        Assert.True(MonitorPaneGeometry.GuidanceMaxWidth(640) <= 400);      // floor of the squeeze
        Assert.True(MonitorPaneGeometry.GuidanceMaxWidth(640) >= 240);
    }
}
