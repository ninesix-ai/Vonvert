// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// FS-001 ~ FS-008: the three text sizes of the fullscreen monitor.
//
// The window is read at arm's length on a second screen, and again as a 400-pixel-wide
// thumbnail inside OBS. One fixed type size cannot serve both, so the user gets a choice -
// and a choice needs the numbers behind it to be deterministic: the middle setting has to
// reproduce exactly what shipped before, the smallest must not quietly drop below anything a
// human can read, and every size the window publishes has to reach the screen.
public sealed class MonitorFontSizesTests
{
    [Fact(DisplayName = "FS-001: standard reproduces the sizes the window shipped with, unchanged")]
    public void FS001_StandardIsTheHistoricalSize()
    {
        // The same promise the palettes make: nobody's monitor changes appearance after an
        // update unless they asked for it.
        var resources = MonitorFontSizes.ResourcesFor(MonitorFontScale.Standard);
        Assert.Equal(13, resources["FsFontMainTitle"]);
        Assert.Equal(12, resources["FsFontPaneTitle"]);
        Assert.Equal(10, resources["FsFontBody"]);
        Assert.Equal(9, resources["FsFontFine"]);
        Assert.Equal(11, resources["FsFontChrome"]);
        Assert.Equal(36, resources["FsFontPitchNote"]);
        Assert.Equal(48, resources["FsFontOverlay"]);
    }

    [Fact(DisplayName = "FS-002: the three sizes are strictly ordered for every role")]
    public void FS002_OrderIsStrict()
    {
        foreach (string key in MonitorFontSizes.ResourcesFor(MonitorFontScale.Standard).Keys)
        {
            double compact = MonitorFontSizes.ResourcesFor(MonitorFontScale.Compact)[key];
            double standard = MonitorFontSizes.ResourcesFor(MonitorFontScale.Standard)[key];
            double large = MonitorFontSizes.ResourcesFor(MonitorFontScale.Large)[key];

            Assert.True(compact < standard, $"{key}: compact {compact} is not smaller than standard {standard}");
            Assert.True(standard < large, $"{key}: large {large} is not bigger than standard {standard}");
        }
    }

    [Fact(DisplayName = "FS-003: nothing gets smaller than the readable floor, even the 9 px fine print")]
    public void FS003_NoSizeFallsBelowTheFloor()
    {
        // The smallest role is the LUFS legend at 9 px; a naive 0.8x would put it at 7.2 and
        // illegible, which is the opposite of what a size control is for.
        foreach (var pair in MonitorFontSizes.ResourcesFor(MonitorFontScale.Compact))
            Assert.True(pair.Value >= MonitorFontSizes.MinimumReadable,
                $"{pair.Key} is {pair.Value} at the compact size; the floor is {MonitorFontSizes.MinimumReadable}");

        Assert.True(MonitorFontSizes.For(MonitorFontScale.Compact, 9) == MonitorFontSizes.MinimumReadable,
            "the floor should bite on exactly the fine print, not on everything");
    }

    [Fact(DisplayName = "FS-004: the large setting is a real difference, not a rounding nudge")]
    public void FS004_LargeIsMeaningfullyBigger()
    {
        double baseBody = MonitorFontSizes.ResourcesFor(MonitorFontScale.Standard)["FsFontBody"];
        double largeBody = MonitorFontSizes.ResourcesFor(MonitorFontScale.Large)["FsFontBody"];
        Assert.True(largeBody / baseBody >= 1.3,
            $"large is only {(largeBody / baseBody - 1) * 100:F0}% bigger; a thumbnail in OBS needs a visible jump");
    }

    [Fact(DisplayName = "FS-005: cycling covers all three and an unknown value returns to standard")]
    public void FS005_CycleAndFallback()
    {
        Assert.Equal(MonitorFontScale.Large, MonitorFontSizes.Next(MonitorFontScale.Standard));
        Assert.Equal(MonitorFontScale.Compact, MonitorFontSizes.Next(MonitorFontScale.Large));
        Assert.Equal(MonitorFontScale.Standard, MonitorFontSizes.Next(MonitorFontScale.Compact));
        Assert.Equal(MonitorFontScale.Standard, MonitorFontSizes.Next((MonitorFontScale)99));
    }

    [Theory(DisplayName = "FS-006: every size has a name in every shipped language")]
    [MemberData(nameof(Languages))]
    public void FS006_NamesExistEverywhere(string lang)
    {
        var ui = ReadUiSection(lang);
        var names = new[] { MonitorFontScale.Compact, MonitorFontScale.Standard, MonitorFontScale.Large }
            .Select(MonitorFontSizes.NameKeyFor).ToArray();

        Assert.Equal(3, names.Distinct().Count());
        var missing = names.Append(MonitorFontSizes.ToggleKey).Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(), $"{lang}: missing text-size copy [{string.Join(", ", missing)}]");
    }

    [Fact(DisplayName = "FS-007: the three offers are distinct words, not three numbers a user has to compare")]
    public void FS007_NamesAreDistinctKeys()
    {
        var keys = new[] { MonitorFontScale.Compact, MonitorFontScale.Standard, MonitorFontScale.Large }
            .Select(MonitorFontSizes.NameKeyFor);
        Assert.DoesNotContain("", keys);
        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact(DisplayName = "FS-008: no published size key is left unbound in the XAML")]
    public void FS008_EverySizeKeyIsUsed()
    {
        // Same rule as the palettes: a setting that changes nothing is worse than no setting,
        // because the button then reports a size the screen is not showing.
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            dir!.FullName, "Vonvert.App", "Windows", "FullscreenVisualizationWindow.xaml"));

        foreach (string key in MonitorFontSizes.ResourcesFor(MonitorFontScale.Standard).Keys)
            Assert.True(xaml.Contains("{DynamicResource " + key + "}"),
                $"{key} is published by the window but no element binds it");

        // The code-behind draws the axis and the scale marks; those have to follow too.
        var code = System.IO.File.ReadAllText(System.IO.Path.Combine(
            dir.FullName, "Vonvert.App", "Windows", "FullscreenVisualizationWindow.xaml.cs"));
        Assert.Contains("MonitorFontSizes.For", code);
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new InvalidOperationException($"embedded {lang}.json not found");
        using var doc = System.Text.Json.JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
