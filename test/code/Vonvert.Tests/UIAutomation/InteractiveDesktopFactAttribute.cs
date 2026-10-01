// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Diagnostics;
using Xunit;

namespace Vonvert.Tests.UIAutomation;

/// <summary>
/// A <see cref="FactAttribute"/> for FlaUI end-to-end tests that need a real,
/// interactive Windows desktop (a visible window station) to launch the app and
/// drive its windows.
///
/// Unlike a plain <c>[Fact(Skip = ...)]</c> — which is skipped unconditionally —
/// this decides at test-discovery time: on a developer machine with a desktop the
/// test still runs; in a headless / service session (Session 0, non-interactive)
/// it is reported as SKIPPED instead of FAILING, so a full <c>dotnet test</c> that
/// forgets the <c>Category!=UIAutomation</c> filter degrades gracefully rather than
/// producing a misleading red. The CI filter remains the primary gate; this is a
/// defense-in-depth guard for the same environment assumption.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class InteractiveDesktopFactAttribute : FactAttribute
{
    public InteractiveDesktopFactAttribute()
    {
        if (!HasInteractiveDesktop())
        {
            Skip = "Requires an interactive desktop session (FlaUI drives real windows); "
                 + "skipped in a headless/service session.";
        }
    }

    /// <summary>
    /// True when the current process can present/drive real windows. Session 0 is
    /// the isolated service window station with no desktop for FlaUI to attach to,
    /// and non-interactive processes likewise have no usable window station.
    /// </summary>
    private static bool HasInteractiveDesktop()
    {
        try
        {
            if (!Environment.UserInteractive) return false;
            if (Process.GetCurrentProcess().SessionId == 0) return false;
            return true;
        }
        catch
        {
            // If the environment probe itself fails, treat as headless and skip.
            return false;
        }
    }
}
