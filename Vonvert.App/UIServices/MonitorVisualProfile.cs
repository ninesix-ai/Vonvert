// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>
/// Which set of surfaces and inks the monitor paints. These are capture-oriented looks,
/// not app themes: the same window has to work as a streamer's on-air graphic, as a
/// readable meter in a bright room, and as a green field for OBS chroma keying.
/// </summary>
public enum MonitorVisualProfile
{
    /// <summary>The shipped look: near-black panels, translucent white ink.</summary>
    Brand,
    /// <summary>Maximum legibility - opaque white on black, yellow for secondary text.</summary>
    HighContrast,
    /// <summary>Standard OBS chroma green behind the picture so the window can be keyed.</summary>
    Chroma,
    /// <summary>Grey instead of purple-blue, for a neutral overlay on any stream.</summary>
    Neutral,
}

/// <summary>
/// The four colours a profile needs. Hex strings, not brushes, so the values can be checked
/// for contrast on a machine with no display (VS-001 ~ VS-007) and so the window stays the
/// only place that touches WPF.
/// </summary>
public readonly record struct MonitorInk(string Pane, string PanelBorder, string Label, string Sub);

/// <summary>Palettes, cycling order and copy keys for <see cref="MonitorVisualProfile"/>.</summary>
public static class MonitorVisualPalettes
{
    /// <summary>The OBS chroma-key default green. Not a taste value: a different green makes
    /// the user retune their key, which is exactly the fiddliness this profile exists to avoid.</summary>
    public const string ChromaGreen = "#00B140";

    private static readonly MonitorVisualProfile[] CycleOrder =
    {
        MonitorVisualProfile.Brand, MonitorVisualProfile.HighContrast,
        MonitorVisualProfile.Chroma, MonitorVisualProfile.Neutral,
    };

    public static MonitorInk For(MonitorVisualProfile profile) => profile switch
    {
        // Deliberately the colours the window shipped with, apart from the waterfall pane:
        // it used pure black while its neighbours used #0A0C12, which made three surfaces
        // read as two. The separator keeps its own faint alpha - a palette must not quietly
        // restyle the default look. VS-007 pins all of it.
        MonitorVisualProfile.HighContrast => new MonitorInk("#000000", "#88FFFFFF", "#FFFFFFFF", "#FFFF00"),
        MonitorVisualProfile.Chroma       => new MonitorInk(ChromaGreen, "#007A2B", "#FFFFFFFF", "#FFFFFFFF"),
        MonitorVisualProfile.Neutral      => new MonitorInk("#14161C", "#55FFFFFF", "#F2FFFFFF", "#CCFFFFFF"),
        _                                 => new MonitorInk("#0A0C12", "#22FFFFFF", "#CCFFFFFF", "#99FFFFFF"),
    };

    /// <summary>Next profile in the cycling order. An unknown value restarts at the shipped
    /// look rather than skipping to whatever happens to be last.</summary>
    public static MonitorVisualProfile Next(MonitorVisualProfile current)
    {
        int index = Array.IndexOf(CycleOrder, current);
        if (index < 0) return CycleOrder[0];
        return CycleOrder[(index + 1) % CycleOrder.Length];
    }

    /// <summary>Translation key naming a profile to the user. Living next to the enum so a new
    /// profile cannot be added without its copy, which is what MonitorGuidanceModel does.</summary>
    public static string NameKeyFor(MonitorVisualProfile profile) => profile switch
    {
        MonitorVisualProfile.HighContrast => "FsColorHighContrast",
        MonitorVisualProfile.Chroma       => "FsColorChroma",
        MonitorVisualProfile.Neutral      => "FsColorNeutral",
        _                                 => "FsColorBrand",
    };

    /// <summary>Label of the button that cycles profiles; its value is the name above.</summary>
    public const string ToggleKey = "FsColorScheme";
}
