// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// VS-001 ~ VS-008: the capture-oriented colour profiles of the fullscreen monitor.
//
// The window is looked at through a camera pipeline: downscaled into an OBS canvas, in a
// bright room, sometimes keyed out over a green field. Contrast here is measurable rather
// than a matter of taste, so it is asserted with the WCAG formula over the actual palette,
// and the shipped look is pinned so the default profile changes nothing on screen.
public sealed class MonitorVisualProfileTests
{
    [Theory(DisplayName = "VS-001: every profile supplies all four colours as hex")]
    [InlineData(MonitorVisualProfile.Brand)]
    [InlineData(MonitorVisualProfile.HighContrast)]
    [InlineData(MonitorVisualProfile.Chroma)]
    [InlineData(MonitorVisualProfile.Neutral)]
    [InlineData((MonitorVisualProfile)42)]
    public void VS001_PalettesAreComplete(MonitorVisualProfile profile)
    {
        var ink = MonitorVisualPalettes.For(profile);
        foreach (string hex in new[] { ink.Pane, ink.PanelBorder, ink.Label, ink.Sub })
            Assert.True(IsHexColor(hex), $"unparsable colour '{hex}' in profile {profile}");
    }

    [Fact(DisplayName = "VS-002: the chroma profile uses OBS's default keying green, not a near-miss")]
    public void VS002_ChromaIsStandardGreen()
    {
        Assert.Equal("#00B140", MonitorVisualPalettes.ChromaGreen);
        Assert.Equal(MonitorVisualPalettes.ChromaGreen, MonitorVisualPalettes.For(MonitorVisualProfile.Chroma).Pane);
    }

    [Fact(DisplayName = "VS-003: high-contrast inks clear 7:1 against their own surface")]
    public void VS003_HighContrastIsActuallyHighContrast()
    {
        var ink = MonitorVisualPalettes.For(MonitorVisualProfile.HighContrast);
        Assert.True(Contrast(ink.Label, ink.Pane) >= 7.0,
            $"high-contrast label is {Contrast(ink.Label, ink.Pane):F1}:1; this profile exists for a bright room and low vision");
        Assert.True(Contrast(ink.Sub, ink.Pane) >= 7.0,
            $"high-contrast secondary ink is {Contrast(ink.Sub, ink.Pane):F1}:1");
    }

    [Theory(DisplayName = "VS-004: brand and neutral inks clear the 4.5:1 body-text floor")]
    [InlineData(MonitorVisualProfile.Brand)]
    [InlineData(MonitorVisualProfile.Neutral)]
    public void VS004_NormalProfilesAreReadable(MonitorVisualProfile profile)
    {
        var ink = MonitorVisualPalettes.For(profile);
        Assert.True(Contrast(ink.Label, ink.Pane) >= 4.5,
            $"{profile} label is {Contrast(ink.Label, ink.Pane):F1}:1 against the panel");
        Assert.True(Contrast(ink.Sub, ink.Pane) >= 4.5,
            $"{profile} secondary ink is {Contrast(ink.Sub, ink.Pane):F1}:1 against the panel");
    }

    [Fact(DisplayName = "VS-005: the chroma ink stays legible to the user while they key it out")]
    public void VS005_ChromaInkStaysVisible()
    {
        // White on keying green is a deliberate compromise: the label plates behind most of
        // this text carry their own dark background, and the field is meant to disappear in
        // OBS. Pinned at a floor so nobody can push it into invisible-in-both-places territory.
        var ink = MonitorVisualPalettes.For(MonitorVisualProfile.Chroma);
        Assert.True(Contrast(ink.Label, ink.Pane) >= 2.5,
            $"chroma label is {Contrast(ink.Label, ink.Pane):F1}:1; below this it is only readable through the key");
    }

    [Fact(DisplayName = "VS-006: cycling visits every profile once and returns to the shipped look")]
    public void VS006_CycleOrderAndWrap()
    {
        var seen = new List<MonitorVisualProfile>();
        var current = MonitorVisualProfile.Brand;
        for (int i = 0; i < 4; i++)
        {
            current = MonitorVisualPalettes.Next(current);
            seen.Add(current);
        }
        Assert.Equal(new[]
        {
            MonitorVisualProfile.HighContrast, MonitorVisualProfile.Chroma,
            MonitorVisualProfile.Neutral, MonitorVisualProfile.Brand,
        }, seen);

        // A value from a newer or older build restarts at the shipped look instead of vanishing.
        Assert.Equal(MonitorVisualProfile.Brand, MonitorVisualPalettes.Next((MonitorVisualProfile)77));
    }

    [Fact(DisplayName = "VS-007: the default profile is the colour set the window shipped with")]
    public void VS007_BrandIsTheHistoricalLook()
    {
        // Guards against a palette edit quietly restyling everybody's monitor: the only
        // intended change is the waterfall pane moving off pure black onto the same
        // near-black as its neighbours, so the three surfaces read as one.
        var ink = MonitorVisualPalettes.For(MonitorVisualProfile.Brand);
        Assert.Equal("#0A0C12", ink.Pane);
        Assert.Equal("#22FFFFFF", ink.PanelBorder);
        Assert.Equal("#CCFFFFFF", ink.Label);
        Assert.Equal("#99FFFFFF", ink.Sub);
    }

    [Theory(DisplayName = "VS-008: every profile has a name in every shipped language")]
    [MemberData(nameof(Languages))]
    public void VS008_ProfileNamesExistEverywhere(string lang)
    {
        var ui = ReadUiSection(lang);
        var names = new[] { MonitorVisualProfile.Brand, MonitorVisualProfile.HighContrast,
                            MonitorVisualProfile.Chroma, MonitorVisualProfile.Neutral }
            .Select(MonitorVisualPalettes.NameKeyFor)
            .ToArray();

        // Four profiles sharing one label would make the button lie about what it shows.
        Assert.Equal(4, names.Distinct().Count());

        var missing = names.Append(MonitorVisualPalettes.ToggleKey).Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(), $"{lang}: missing colour copy [{string.Join(", ", missing)}]");
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static bool IsHexColor(string hex) =>
        hex is not null && (hex.Length == 7 || hex.Length == 9) && hex[0] == '#'
        && hex.Skip(1).All(Uri.IsHexDigit);

    private static double Contrast(string foreground, string background)
    {
        // Translucent inks are specified with an alpha channel; what is actually seen is the
        // blend over the surface, and that is what has to pass the contrast check.
        double[] fg = Blend(ToRgb(foreground), ToRgb(background));
        double[] bg = Channels(ToRgb(background));
        return (Luminance(fg) + 0.05) / (Luminance(bg) + 0.05);
    }

    /// <summary>"#RGB"-free by design: every palette colour is written as #RRGGBB or #AARRGGBB.</summary>
    private static int[] ToRgb(string hex) =>
        Enumerable.Range(1, hex.Length - 1)
            .Where(i => i % 2 == 1)
            .Select(i => Convert.ToInt32(hex.Substring(i, 2), 16))
            .ToArray();

    private static double[] Channels(int[] rgb) =>
        rgb.Skip(rgb.Length == 4 ? 1 : 0).Select(c => (double)c).ToArray();

    private static double[] Blend(int[] argb, int[] background)
    {
        double a = argb.Length == 4 ? argb[0] / 255.0 : 1.0;
        double[] fg = Channels(argb);
        double[] bg = Channels(background);
        return Enumerable.Range(0, 3).Select(i => a * fg[i] + (1 - a) * bg[i]).ToArray();
    }

    private static double Luminance(double[] channels)
    {
        double[] linear = channels.Select(c =>
        {
            double s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }).ToArray();

        return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2];
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
