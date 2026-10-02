// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <summary>
/// How the monitor responds to the accessibility signal Windows already publishes.
///
/// Two decisions live here because both are about precedence rather than about WPF: which
/// palette to open with, and whether the idle beat is allowed to take the pointer away. A
/// user who set a palette on purpose outranks the system setting - otherwise the profile
/// button would be overwritten the next time the window opens, which makes it a control that
/// lies. Everything else defers to the system.
/// </summary>
public static class MonitorAccessibilityPolicy
{
    public static MonitorVisualProfile InitialProfile(
        bool systemHighContrast, MonitorVisualProfile stored, bool userChoseProfile)
    {
        if (userChoseProfile) return stored;
        if (systemHighContrast) return MonitorVisualProfile.HighContrast;
        return stored;      // untouched first run keeps exactly the look the window shipped with
    }

    /// <summary>
    /// Hiding the cursor is what makes a captured picture clean, and it is the only way a
    /// low-vision user keeps track of their pointer in a borderless window. Under high
    /// contrast the second group wins: the pointer stays.
    /// </summary>
    public static bool ShouldHideCursorWhenIdle(bool systemHighContrast) => !systemHighContrast;
}
