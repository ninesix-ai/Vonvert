// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Linq;

namespace Vonvert.App.UIServices;

/// <summary>
/// Declarative, UI-free policy for the shared section-header chrome that sits
/// above the tab control. Each header control declares the tabs it belongs to;
/// a control is visible ONLY on those tabs. This mirrors the persona/feature
/// visibility registry for the tab axis, so that adding a new tab or a new
/// header widget cannot silently "leak" a control onto a tab it does not belong
/// to (the failure mode where imperative <c>SelectedIndex == 0</c> checks drift).
///
/// The policy is fail-closed: an unknown control or an unlisted tab yields
/// hidden, so a forgotten registration shows as "missing" (safe) rather than
/// "shown everywhere" (bug).
/// </summary>
public static class TabChromePolicy
{
    /// <summary>Ordered tabs of the main window (values match TabControl.SelectedIndex).</summary>
    public enum AppTab { Voices = 0, Settings = 1, About = 2, Recording = 3 }

    /// <summary>Shared section-header controls whose visibility is tab-scoped.</summary>
    public enum HeaderControl { AbCompare }

    // Single source of truth: control -> the tabs it is visible on.
    private static readonly Dictionary<HeaderControl, AppTab[]> OwnerTabs = new()
    {
        [HeaderControl.AbCompare] = new[] { AppTab.Voices },
    };

    /// <summary>Map a TabControl.SelectedIndex to an <see cref="AppTab"/>; false if out of range.</summary>
    public static bool TryFromIndex(int selectedIndex, out AppTab tab)
    {
        if (Enum.IsDefined(typeof(AppTab), selectedIndex))
        {
            tab = (AppTab)selectedIndex;
            return true;
        }
        tab = default;
        return false;
    }

    /// <summary>Whether <paramref name="control"/> should be visible on <paramref name="tab"/> (fail-closed).</summary>
    public static bool IsControlVisible(HeaderControl control, AppTab tab)
        => OwnerTabs.TryGetValue(control, out var tabs) && Array.IndexOf(tabs, tab) >= 0;

    /// <summary>All known header controls (for exhaustive testing).</summary>
    public static IReadOnlyCollection<HeaderControl> Controls => OwnerTabs.Keys.ToList();

    /// <summary>All tabs (for exhaustive testing).</summary>
    public static IReadOnlyList<AppTab> Tabs => (AppTab[])Enum.GetValues(typeof(AppTab));
}
