// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;

namespace Vonvert.App.UIServices;

/// <summary>The three double-clickable panels of the fullscreen monitor.
/// The pitch badges are an overlay on the waterfall, not a pane.</summary>
public enum MonitorPane { Waterfall, Waveform, Loudness }

/// <summary>
/// Pure state for the monitor's "maximize one pane" interaction: at most one
/// pane is ever maximized; toggling the same pane restores the grid. Lives
/// WPF-free so the interaction invariant is unit-testable without a dispatcher.
/// </summary>
public sealed class PaneLayoutModel
{
    public MonitorPane? Maximized { get; private set; }

    public void Toggle(MonitorPane pane)
        => Maximized = Maximized == pane ? null : pane;

    /// <summary>
    /// Which panels are currently hidden because one fills the window. The UI shows a
    /// clickable strip per entry, so getting back does not require knowing that another
    /// double-click is the way out - the gesture stops being a secret the moment it has a
    /// visible alternative.
    /// </summary>
    public IReadOnlyList<MonitorPane> Collapsed()
    {
        if (Maximized is null) return Array.Empty<MonitorPane>();
        var list = new List<MonitorPane>();
        foreach (MonitorPane pane in Enum.GetValues<MonitorPane>())
            if (pane != Maximized.Value)
                list.Add(pane);
        return list;
    }
}
