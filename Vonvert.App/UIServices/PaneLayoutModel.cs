// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

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
}
