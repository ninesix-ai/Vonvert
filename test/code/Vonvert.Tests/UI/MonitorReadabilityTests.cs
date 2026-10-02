// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Vonvert.Tests.UI;

// RD-001 ~ RD-006: source-level readability and accessibility guards for the monitor.
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

    [Fact(DisplayName = "RD-005: the overlay axis cannot swallow the drag and double-click gestures")]
    public void RD005_AxisOverlayIsTransparentToHitTesting()
    {
        // The axis covers the whole waterfall pane. If it took mouse input, dragging the
        // window and double-clicking to maximize would silently stop working on most of
        // the pane - the two gestures that used to be hard enough to find anyway.
        var axis = Regex.Match(MonitorXaml, @"<Canvas x:Name=""AxisCanvas""[^>]*?/?>");
        Assert.True(axis.Success, "AxisCanvas not found in the monitor XAML - update this guard");
        Assert.Contains("IsHitTestVisible=\"False\"", axis.Value);
    }

    [Fact(DisplayName = "RD-006: every panel and every control in the monitor is named for a screen reader")]
    public void RD006_ControlsAreNamed()
    {
        // The rest of the app already names its controls (Acc* on the tab strip, the bottom
        // bar, the sidebar); this window had none, so a screen reader announced an unnamed
        // rectangle. A count, not a sample, because new buttons arrive with names or not.
        int named = Regex.Matches(MonitorXaml, @"AutomationProperties\.Name=""").Count;
        Assert.True(named >= 13,
            $"only {named} AutomationProperties.Name bindings in the monitor; panels and every control need one");
    }

    /// <summary>
    /// The only text left selectable in this window: readings a user might quote to support.
    /// Anything else that can be highlighted competes with dragging the window.
    /// </summary>
    private static readonly string[] CopyableReadings =
    {
        "FsPitchNoteText", "FsPitchCentsText", "FsPitchFreqText",
        "LufsTruePeakText", "LufsIntegratedText",
    };

    [Fact(DisplayName = "RD-007: labels and wording are plain text; only numeric readings stay selectable")]
    public void RD007_OnlyReadingsAreSelectable()
    {
        // Titles, subtitles and the Esc hint carry no information worth copying, and every
        // one of them used to start a text selection the moment the user grabbed the window
        // to move it - a blue smear across the panel during a drag.
        var offenders = new List<string>();
        foreach (Match m in Regex.Matches(MonitorXaml, @"<vc:SelectableTextBlock(?=[\s/>])(?<attrs>[^>]*?)(?:/>|>)"))
        {
            var name = Regex.Match(m.Groups["attrs"].Value, @"x:Name=""(?<n>[^""]+)""");
            if (!name.Success || !CopyableReadings.Contains(name.Groups["n"].Value))
                offenders.Add(name.Success ? name.Groups["n"].Value : $"unbound text at char {m.Index}");
        }
        Assert.False(offenders.Any(),
            $"selectable wording in the monitor (drag and select fight over the same press): {string.Join(", ", offenders)}");

        int plainLabels = Regex.Matches(MonitorXaml, @"<TextBlock Text=""\{Binding Source=\{x:Static local:LocalizationManager").Count;
        Assert.True(plainLabels >= 6,
            $"expected the panel wording to be plain TextBlock, found {plainLabels}; wording must not be selectable");
    }

    [Fact(DisplayName = "RD-008: a press that starts on copyable text does not drag the window")]
    public void RD008_DragYieldsToCopyableText()
    {
        // The window hook runs even when a child claimed the event, so without this guard
        // both the drag and the selection happen at once.
        var drag = Regex.Match(MonitorCode, @"private void OnWindowDragStart[\s\S]*?\n    \}");
        Assert.True(drag.Success, "OnWindowDragStart not found - update this guard");
        Assert.True(drag.Value.Contains("SelectableTextBlock"),
            "a press on a reading the user means to copy must not move the window");
    }

    [Fact(DisplayName = "RD-009: the capture palettes are consumed, not just computed")]
    public void RD009_PaletteIsActuallyBound()
    {
        // A palette nobody reads is worse than no palette at all: the profile button would
        // appear to work, report a new name, and leave the picture untouched. Both ends are
        // pinned - the window must publish each key and the XAML must bind it.
        foreach (string key in new[] { "FsPaneBrush", "FsPanelBorderBrush", "FsLabelBrush", "FsSubBrush" })
        {
            Assert.True(MonitorCode.Contains($"Resources[\"{key}\"]"),
                $"{key} is never published by the window");
            Assert.True(Regex.Matches(MonitorXaml, @"\{DynamicResource " + key + @"\}").Count >= 1,
                $"{key} is published but bound nowhere in the XAML");
        }

        Assert.True(Regex.Matches(MonitorXaml, @"\{DynamicResource FsPaneBrush\}").Count >= 3,
            "all three panes should follow the palette, not just one");
    }
}
