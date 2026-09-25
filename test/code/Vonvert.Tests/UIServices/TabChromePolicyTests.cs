// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Traversal guard for the tab-axis header chrome policy. It enumerates every
// (header control × tab) pair and asserts the exact expected visibility matrix,
// so that adding a new tab or a new header control without registering it (the
// class of bug where Auto-pitch leaked onto Settings/About) is caught here as a
// failing assertion rather than at runtime.

namespace Vonvert.Tests.UIServices;

using System;
using System.Linq;
using Vonvert.App.UIServices;
using Xunit;

public class TabChromePolicyTests
{
    private static readonly TabChromePolicy.AppTab[] AllTabs =
        (TabChromePolicy.AppTab[])Enum.GetValues(typeof(TabChromePolicy.AppTab));

    [Theory]
    // Voice-scoped chrome is visible ONLY on the Voices tab (fail-closed everywhere else).
    [InlineData(TabChromePolicy.HeaderControl.AbCompare, TabChromePolicy.AppTab.Voices,   true)]
    [InlineData(TabChromePolicy.HeaderControl.AbCompare, TabChromePolicy.AppTab.Settings, false)]
    [InlineData(TabChromePolicy.HeaderControl.AbCompare, TabChromePolicy.AppTab.About,    false)]
    [InlineData(TabChromePolicy.HeaderControl.AbCompare, TabChromePolicy.AppTab.Recording, false)]
    [InlineData(TabChromePolicy.HeaderControl.AbCompare, TabChromePolicy.AppTab.Soundboard, false)]
    public void IsControlVisible_MatchesExpectedMatrix(
        TabChromePolicy.HeaderControl control, TabChromePolicy.AppTab tab, bool expected)
    {
        Assert.Equal(expected, TabChromePolicy.IsControlVisible(control, tab));
    }

    [Fact]
    public void EveryControlTabPair_IsAccountedFor_NoPhantomVisibility()
    {
        // Safety net that grows automatically: for any control the policy knows,
        // it must be visible on at least one tab (else it is dead UI), and never
        // visible on a non-Voices tab unless explicitly added to the matrix above.
        foreach (var control in TabChromePolicy.Controls)
        {
            var visibleTabs = AllTabs.Where(t => TabChromePolicy.IsControlVisible(control, t)).ToList();
            Assert.NotEmpty(visibleTabs);                                   // not dead UI
            Assert.All(visibleTabs, t => Assert.Equal(TabChromePolicy.AppTab.Voices, t)); // only Voices today
        }
    }

    [Theory]
    [InlineData(0, TabChromePolicy.AppTab.Voices)]
    [InlineData(1, TabChromePolicy.AppTab.Settings)]
    [InlineData(2, TabChromePolicy.AppTab.About)]
    [InlineData(3, TabChromePolicy.AppTab.Recording)]
    [InlineData(4, TabChromePolicy.AppTab.Soundboard)]
    public void TryFromIndex_Valid_Maps(int index, TabChromePolicy.AppTab expected)
    {
        Assert.True(TabChromePolicy.TryFromIndex(index, out var tab));
        Assert.Equal(expected, tab);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void TryFromIndex_OutOfRange_ReturnsFalse(int index)
    {
        Assert.False(TabChromePolicy.TryFromIndex(index, out _));   // caller then hides chrome (fail-closed)
    }

    [Fact]
    public void TabIndexMatchesTabControlOrder()
    {
        // AppTab values are the TabControl.SelectedIndex; keep them contiguous from 0.
        Assert.Equal(0, (int)TabChromePolicy.AppTab.Voices);
        Assert.Equal(AllTabs.Length, TabChromePolicy.Tabs.Count);
    }

    [Fact]
    public void Soundboard_IsLastContiguousTab()
    {
        // The new tab is appended as the max index so the existing 0/1/2 chrome matrix
        // and Onboarding's hardcoded SelectedIndex stay intact (the append-only invariant).
        Assert.Equal(4, (int)TabChromePolicy.AppTab.Soundboard);
        Assert.Equal(5, AllTabs.Length);
        var indices = AllTabs.Select(t => (int)t).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, indices);
    }
}
