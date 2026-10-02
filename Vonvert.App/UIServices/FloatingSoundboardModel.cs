// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Collections.Generic;

namespace Vonvert.App.UIServices;

/// <summary>
/// UI-free logic behind the floating mini-player: which pads to show and in what order.
/// Kept separate from the WPF window so the selection rule is unit-testable without STA.
/// </summary>
public static class FloatingSoundboardModel
{
    /// <summary>
    /// Resolve the pinned ids that still exist in the catalogue, in pin order, de-duplicated.
    /// A pinned id whose sound was removed is skipped (guards against stale favorites).
    /// </summary>
    public static IReadOnlyList<string> OrderPinned(
        IReadOnlyList<string> pinnedIds, IReadOnlyCollection<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(pinnedIds);
        ArgumentNullException.ThrowIfNull(existingIds);
        var existing = new HashSet<string>(existingIds, StringComparer.Ordinal);
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in pinnedIds)
            if (existing.Contains(id) && seen.Add(id))
                ordered.Add(id);
        return ordered;
    }
}
