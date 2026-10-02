// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Vonvert.Tests.UI;

// RD-001 ~ RD-004: source-level readability guards for the fullscreen monitor.
//
// This window is built to be captured: OBS downscales it, a second screen is viewed
// from a distance, and a bright room washes out thin text. Every label used to sit at
// 33-40% alpha white, and the pitch badge put purple text on a purple plate - invisible
// in exactly the situations the feature exists for. Same idea as the SYNC-001/002
// gates: assert on the source so a regression cannot be merged quietly.
public sealed class MonitorReadabilityTests
{
    private const int MinTextAlpha = 0x80;   // white on black at 0x80 is about 5.4:1

    private static readonly Regex ForegroundAlpha =
        new(@"Foreground=""#(?<a>[0-9A-Fa-f]{2})[0-9A-Fa-f]{6}""", RegexOptions.Compiled);

    private static readonly Regex BadgeBackground =
        new(@"x:Name=""(?<name>FsPitch\w*Badge)""[^>]*?Background=""#(?<a>[0-9A-Fa-f]{2})[0-9A-Fa-f]{6}""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }

    private static string MonitorXaml =>
        File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Windows", "FullscreenVisualizationWindow.xaml"));

    private static string MonitorCode =>
        File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Windows", "FullscreenVisualizationWindow.xaml.cs"));

    [Fact(DisplayName = "RD-001: no monitor label is drawn below the minimum text alpha")]
    public void RD001_TextAlphaFloor()
    {
        var thin = new List<string>();
        foreach (Match m in ForegroundAlpha.Matches(MonitorXaml))
        {
            int alpha = Convert.ToInt32(m.Groups["a"].Value, 16);
            if (alpha < MinTextAlpha)
                thin.Add($"#{m.Groups["a"].Value}.. at char {m.Index}");
        }
        Assert.True(thin.Count == 0,
            $"monitor labels below alpha 0x{MinTextAlpha:X2} disappear under OBS downscaling and in a bright room: {string.Join(", ", thin)}");
    }

    [Fact(DisplayName = "RD-002: the pitch badges keep a solid plate so the colored note text stays readable")]
    public void RD002_BadgePlatesAreOpaque()
    {
        var seen = 0;
        foreach (Match m in BadgeBackground.Matches(MonitorXaml))
        {
            seen++;
            int alpha = Convert.ToInt32(m.Groups["a"].Value, 16);
            Assert.True(alpha >= 0xCC,
                $"{m.Groups["name"].Value} plate is #{m.Groups["a"].Value}..; the note color is set per deviation, so the plate has to be solid");
        }
        Assert.True(seen >= 2, $"expected the note and cents badge plates in the XAML, found {seen}");
    }

    [Fact(DisplayName = "RD-003: no state button is recolored imperatively (a local value outranks template triggers)")]
    public void RD003_NoImperativeButtonForeground()
    {
        // A local value beats the template's IsMouseOver/IsFocused setters, which is how the
        // dry/wet taps lost their hover feedback. Button state must be expressed by
        // switching styles. Data-driven label colors (the pitch note) are not buttons.
        var hits = Regex.Matches(MonitorCode, @"^\s*\w*Btn\w*\.Foreground\s*=", RegexOptions.Multiline);
        Assert.True(hits.Count == 0,
            $"{hits.Count} imperative button Foreground assignment(s) in the monitor; use SetResourceReference(StyleProperty, ...) instead");
    }

    [Fact(DisplayName = "RD-004: strip sizes come from the geometry rules, not from hardcoded pixels")]
    public void RD004_NoHardcodedStripSizes()
    {
        // 140 and 150 were the original fixed strips. A literal back in this file would
        // silently undo proportional layout at every window size except 1280x720.
        var literals = Regex.Matches(MonitorCode, @"new GridLength\((140|150)\)");
        Assert.True(literals.Count == 0,
            $"{literals.Count} hardcoded strip size(s) in the monitor; use MonitorPaneGeometry.Compute instead");
    }
}
