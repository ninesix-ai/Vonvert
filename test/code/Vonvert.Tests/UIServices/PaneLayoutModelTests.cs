// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// PL-001 ~ PL-004: at most one pane is maximized; toggling the same pane restores.
public sealed class PaneLayoutModelTests
{
    [Fact(DisplayName = "PL-001: initial state has no maximized pane")]
    public void PL001_InitialNone() => Assert.Null(new PaneLayoutModel().Maximized);

    [Fact(DisplayName = "PL-002: toggling a pane maximizes it and supersedes the previous one")]
    public void PL002_SingleMaximizeInvariant()
    {
        var m = new PaneLayoutModel();
        m.Toggle(MonitorPane.Waterfall);
        Assert.Equal(MonitorPane.Waterfall, m.Maximized);
        m.Toggle(MonitorPane.Loudness);
        Assert.Equal(MonitorPane.Loudness, m.Maximized);   // never two at once
    }

    [Fact(DisplayName = "PL-003: double-toggle of the same pane restores the grid")]
    public void PL003_ToggleTwiceRestores()
    {
        var m = new PaneLayoutModel();
        m.Toggle(MonitorPane.Waveform);
        m.Toggle(MonitorPane.Waveform);
        Assert.Null(m.Maximized);
    }

    [Fact(DisplayName = "PL-004: nothing is collapsed in the grid, and exactly the other two panes when one fills the window")]
    public void PL004_CollapsedPanes()
    {
        var m = new PaneLayoutModel();
        Assert.Empty(m.Collapsed());                      // nothing hidden -> no strips

        m.Toggle(MonitorPane.Loudness);
        var hidden = m.Collapsed();
        Assert.Equal(2, hidden.Count);
        Assert.DoesNotContain(MonitorPane.Loudness, hidden);   // the visible one is not offered back
        Assert.Contains(MonitorPane.Waterfall, hidden);
        Assert.Contains(MonitorPane.Waveform, hidden);
    }
}
