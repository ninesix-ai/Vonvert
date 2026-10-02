// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UI;

// HG-001 ~ HG-006: the monitor's hidden gestures must be discoverable without being told.
//
// Double-click to fill a panel, double-click again to restore, and drag-anywhere-to-move
// were the whole interaction model, and none of them were visible. The first-run tour and
// the help card only appear on demand, so a returning user who skipped them, or anyone on
// a second machine, still met a window that looked inert. These gates require a real
// clickable affordance per panel, a way back that is not a secret gesture, and copy that
// exists in every shipped language.
public sealed class MonitorHintTests
{
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

    [Fact(DisplayName = "HG-001: every panel carries a visible hint chip naming the gesture")]
    public void HG001_EveryPanelHasAHintChip()
    {
        int chips = Regex.Matches(MonitorXaml, @"Style=""\{StaticResource HintChip\}""").Count;
        Assert.True(chips >= 3, $"only {chips} hint chips; each of the three panels needs one");

        foreach (string binding in new[] { "FsWaterfallHint", "FsLoudnessHint", "FsWaveformHint" })
            Assert.Contains(binding, MonitorXaml);
    }

    [Fact(DisplayName = "HG-002: the hint chip is a control that performs the action, not just a label")]
    public void HG002_ChipsAreClickable()
    {
        // A tooltip on text nobody can reach teaches nothing; clicking the chip must do the
        // same thing as the gesture it advertises.
        Assert.Contains("Style TargetType=\"Button\" x:Key=\"HintChip\"", MonitorXaml);
        var handlers = Regex.Matches(MonitorCode, @"private void (\w+Hint_Click)\(");
        Assert.True(handlers.Count >= 3,
            $"expected a click handler per panel chip, found {handlers.Count}");
        Assert.Contains("SetMonitorMaximizedPane", MonitorCode);   // chip wording follows state
    }

    [Fact(DisplayName = "HG-003: a filled panel can be left with one click, not a secret second double-click")]
    public void HG003_BackRouteIsVisible()
    {
        Assert.Contains("x:Name=\"CollapseBar\"", MonitorXaml);
        Assert.Contains("FsExpandHint", MonitorXaml);
        var model = typeof(PaneLayoutModel).GetMethod("Collapsed");
        Assert.NotNull(model);   // the strips are driven by the model, not guessed in the view
    }

    [Fact(DisplayName = "HG-004: the gesture pill appears on open, fades itself away, and cannot eat a click")]
    public void HG004_GesturePillIsTemporaryAndTransparent()
    {
        Assert.Contains("x:Name=\"GesturePill\"", MonitorXaml);
        Assert.Contains("FsGestureHint", MonitorXaml);
        Assert.Contains("GesturePill.BeginAnimation", MonitorCode);   // fades out by itself
        var pill = Regex.Match(MonitorXaml, @"<Border x:Name=""GesturePill""[^>]*>");
        Assert.True(pill.Success, "GesturePill not found - update this guard");
        Assert.Contains("IsHitTestVisible=\"False\"", pill.Value);    // never blocks drag or double-click
    }

    [Theory(DisplayName = "HG-005: all discoverability copy exists in every shipped language")]
    [MemberData(nameof(Languages))]
    public void HG005_HintCopyIsTranslatedEverywhere(string lang)
    {
        var ui = ReadUiSection(lang);
        var required = new[] { "FsDblClickHint", "FsDblClickRestore", "FsExpandHint", "FsGestureHint",
                               "FsAppearance" };
        var missing = required.Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(), $"{lang}: missing hint keys [{string.Join(", ", missing)}]");
    }

    [Fact(DisplayName = "HG-006: hints use words, not emoji - the layered transparent window renders them as blank boxes")]
    public void HG006_NoEmojiInHints()
    {
        // WPF will not colour emoji here and COLR glyphs fall back to silhouettes, so an
        // emoji affordance would be an unreadable box on air. Emoji above U+FFFF can only
        // appear in a .NET string as a surrogate pair, so that is what gets detected.
        var suspicious = MonitorXaml
            .Where(c => char.IsHighSurrogate(c) || c == '\uFE0F' || c == '\u2B50' || c == '\u2705' || c == '\u274C')
            .ToList();
        Assert.False(suspicious.Any(),
            $"non-textual glyph(s) in the monitor XAML: {string.Join(", ", suspicious.Select(c => $"U+{(int)c:X4}"))}");
    }

    [Fact(DisplayName = "HG-007: every name the monitor XAML binds to actually exists on LocalizationManager")]
    public void HG007_BoundNamesResolve()
    {
        // A mistyped Path= is invisible at build time and shows up as a blank label while
        // the stream is live, which is the worst possible moment to discover a hint.
        var lm = typeof(LocalizationManager);
        var bound = Regex.Matches(MonitorXaml, @"Path=(\w+)").Select(m => m.Groups[1].Value).Distinct();
        var missing = bound.Where(name => lm.GetProperty(name) is null).ToList();
        Assert.False(missing.Any(),
            $"bound but not a LocalizationManager property: {string.Join(", ", missing)}");
        Assert.True(bound.Count() >= 40, $"only {bound.Count()} bound names; the monitor should bind its wording, not hard-code it");
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
