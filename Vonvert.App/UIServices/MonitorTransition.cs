// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;

namespace Vonvert.App.UIServices;

/// <summary>
/// What a layout change should look like. Pure on purpose: the window gets the answer as a
/// list of panels to ease in, so the tricky part - never fading a panel that was already
/// showing, which would make the picture blink - is something a test can hold (TR-001 ~
/// TR-005) instead of an if-chain inside the render path.
/// </summary>
public static class MonitorTransition
{
    /// <summary>Long enough to read as a move, short enough that a live switch never lags.</summary>
    public const int DefaultDurationMs = 150;

    /// <summary>
    /// 0 means "no animation" for the caller. WPF has no standard reduce-motion signal, so
    /// this is driven by the app's own setting rather than by an inferred system one.
    /// </summary>
    public static int DurationMs(bool reduceMotion) => reduceMotion ? 0 : DefaultDurationMs;

    /// <summary>Panels that went from hidden to visible by this layout change.</summary>
    public static IReadOnlyList<MonitorPane> PanesToFade(MonitorPane? before, MonitorPane? after)
    {
        var was = VisibleIn(maximized: before);
        var now = VisibleIn(maximized: after);

        var faded = new List<MonitorPane>();
        foreach (MonitorPane pane in Enum.GetValues<MonitorPane>())
            if (now.Contains(pane) && !was.Contains(pane))
                faded.Add(pane);
        return faded;
    }

    private static HashSet<MonitorPane> VisibleIn(MonitorPane? maximized) => maximized is null
        ? new HashSet<MonitorPane>(Enum.GetValues<MonitorPane>())
        : new HashSet<MonitorPane> { maximized.Value };
}
