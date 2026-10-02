// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;

namespace Vonvert.App.UIServices;

/// <summary>
/// How big the monitor's text is. The window gets read two ways at once: at arm's length on
/// a second screen, and as a thumbnail inside OBS where everything is halved. One fixed size
/// serves neither, so the user chooses.
/// </summary>
public enum MonitorFontScale
{
    /// <summary>Tighter than shipped, for a small window or a busy overlay.</summary>
    Compact,
    /// <summary>Exactly the sizes the window shipped with. The default.</summary>
    Standard,
    /// <summary>Clearly bigger, for a monitor seen from a distance or downscaled in a scene.</summary>
    Large,
}

/// <summary>
/// The type scale, as numbers rather than as WPF. Sizes live here so the rules can be tested
/// without a display (FS-001 ~ FS-005): the middle setting must reproduce what shipped before,
/// the smallest must not go under a readable floor, and the largest must be a real difference.
/// The window turns the table into resources; the XAML binds them.
/// </summary>
public static class MonitorFontSizes
{
    // The role sizes at the standard setting, kept identical to the values the window shipped
    // with. FS-001 pins them, so a refactor cannot quietly restyle everybody on upgrade.
    public const double MainTitle = 13;    // waterfall pane heading and the guidance band
    public const double Heading = 14;      // help card heading
    public const double PaneTitle = 12;    // loudness and waveform headings
    public const double Readout = 12;      // true peak, integrated, pitch legend
    public const double Chrome = 11;       // toolbar buttons
    public const double Body = 10;         // subtitles, source name, hint chips, pill, small buttons
    public const double Fine = 9;          // LUFS pointer legend lines
    public const double PitchFreq = 16;    // Hz readout
    public const double PitchCents = 20;   // cents readout
    public const double PitchNote = 36;    // note badge
    public const double Overlay = 48;      // preset-name fade

    /// <summary>Nothing is drawn smaller than this, however tight the setting. Below about
    /// 8 px the hint text stops being text and becomes texture.</summary>
    public const double MinimumReadable = 8;

    private const double CompactFactor = 0.85;
    private const double LargeFactor = 1.35;

    public static double Factor(this MonitorFontScale scale) => scale switch
    {
        MonitorFontScale.Compact => CompactFactor,
        MonitorFontScale.Large   => LargeFactor,
        _                        => 1.0,      // standard, and any value from another build
    };

    /// <summary>A role size at a given setting, never below the floor.</summary>
    public static double For(MonitorFontScale scale, double baseSize) =>
        Math.Max(MinimumReadable, Math.Round(baseSize * scale.Factor(), 1));

    /// <summary>
    /// The resources the window publishes, keyed by the names the XAML binds. One table, so
    /// adding a role cannot leave the window and the layout pass disagreeing about a size.
    /// </summary>
    public static IReadOnlyDictionary<string, double> ResourcesFor(MonitorFontScale scale) =>
        new Dictionary<string, double>
        {
            ["FsFontMainTitle"] = For(scale, MainTitle),
            ["FsFontHeading"] = For(scale, Heading),
            ["FsFontPaneTitle"] = For(scale, PaneTitle),
            ["FsFontReadout"] = For(scale, Readout),
            ["FsFontChrome"] = For(scale, Chrome),
            ["FsFontBody"] = For(scale, Body),
            ["FsFontFine"] = For(scale, Fine),
            ["FsFontPitchFreq"] = For(scale, PitchFreq),
            ["FsFontPitchCents"] = For(scale, PitchCents),
            ["FsFontPitchNote"] = For(scale, PitchNote),
            ["FsFontOverlay"] = For(scale, Overlay),
        };

    /// <summary>Standard is the safest place to land for a value this build does not know.</summary>
    public static MonitorFontScale Next(MonitorFontScale current) => current switch
    {
        MonitorFontScale.Standard => MonitorFontScale.Large,
        MonitorFontScale.Large    => MonitorFontScale.Compact,
        MonitorFontScale.Compact  => MonitorFontScale.Standard,
        _                         => MonitorFontScale.Standard,
    };

    /// <summary>Cycle order a user can learn: normal, then bigger, then tighter.</summary>
    public static string NameKeyFor(MonitorFontScale scale) => scale switch
    {
        MonitorFontScale.Compact => "FsFontCompact",
        MonitorFontScale.Large   => "FsFontLarge",
        _                        => "FsFontStandard",
    };

    /// <summary>Label of the button that cycles sizes; carries {0}.</summary>
    public const string ToggleKey = "FsFontScale";
}
