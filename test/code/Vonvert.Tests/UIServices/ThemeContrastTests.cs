// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Vonvert.App.UIServices;
using Xunit;

namespace Vonvert.Tests.UIServices;

/// <summary>
/// Regression guard that freezes the manual WCAG contrast audit into CI: every
/// shipped palette must clear a minimum contrast ratio for its important
/// foreground/background pairs. A theme swap that leaves text or a control hue
/// illegible on some palette fails here, not on a human's screen.
///
/// Contrast is computed from the raw #AARRGGBB strings (relative luminance per
/// WCAG 2.x), so this needs no WPF brushes and runs in the headless test host.
/// </summary>
public sealed class ThemeContrastTests
{
    // (foreground, background, minimum ratio, label). 4.5:1 = WCAG AA body text;
    // 3.0:1 = large / graphical objects. Pairs mirror where each token is consumed.
    private static readonly (Func<ThemeDefinition, string> Fg, Func<ThemeDefinition, string> Bg, double Min, string Label)[] Pairs =
    {
        (t => t.TextPrimary, t => t.Bg, 4.5, "textPrimary/bg"),
        (t => t.TextPrimary, t => t.Surface, 4.5, "textPrimary/surface"),
        (t => t.TextPrimary, t => t.SurfaceLift, 4.5, "textPrimary/surfaceLift"),
        (t => t.TextPrimary, t => t.TileSurface, 4.5, "textPrimary/tileSurface"),
        (t => t.TextSub, t => t.Bg, 4.5, "textSub/bg"),
        (t => t.TextSub, t => t.Surface, 4.5, "textSub/surface"),
        (t => t.TextSub, t => t.SurfaceLift, 4.5, "textSub/surfaceLift"),
        (t => t.CtrlText, t => t.CtrlSurface, 4.5, "ctrlText/ctrlSurface"),
        (t => t.Accent, t => t.Bg, 3.0, "accent/bg"),
        (t => t.PowerOn, t => t.Bg, 3.0, "powerOn/bg"),
        (t => t.PowerOff, t => t.Bg, 3.0, "powerOff/bg"),
        (t => t.Warning, t => t.Bg, 3.0, "warning/bg"),
        (t => t.IconVoices, t => t.Bg, 3.0, "iconVoices/bg"),
        (t => t.IconRecord, t => t.Bg, 3.0, "iconRecord/bg"),
        (t => t.IconSettings, t => t.Bg, 3.0, "iconSettings/bg"),
        (t => t.IconAbout, t => t.Bg, 3.0, "iconAbout/bg"),
        (t => t.IconSoundboard, t => t.Bg, 3.0, "iconSoundboard/bg"),
        (t => t.IconCatDrums, t => t.TileSurface, 3.0, "iconCatDrums/tileSurface"),
        (t => t.IconCatTones, t => t.TileSurface, 3.0, "iconCatTones/tileSurface"),
        (t => t.IconCatSFX, t => t.TileSurface, 3.0, "iconCatSFX/tileSurface"),
        (t => t.IconCatMemes, t => t.TileSurface, 3.0, "iconCatMemes/tileSurface"),
        (t => t.IconCatMusic, t => t.TileSurface, 3.0, "iconCatMusic/tileSurface"),
        (t => t.IconCatRetro, t => t.TileSurface, 3.0, "iconCatRetro/tileSurface"),
        (t => t.IconCatAmbient, t => t.TileSurface, 3.0, "iconCatAmbient/tileSurface"),
    };

    private static double Luminance(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8) h = h.Substring(2); // drop AA, keep RRGGBB
        static double Chan(string twoHex)
        {
            double c = Convert.ToInt32(twoHex, 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Chan(h.Substring(0, 2)) + 0.7152 * Chan(h.Substring(2, 2)) + 0.0722 * Chan(h.Substring(4, 2));
    }

    private static double Contrast(string a, string b)
    {
        double la = Luminance(a), lb = Luminance(b);
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    [Fact(DisplayName = "THM-CONTRAST-001: every shipped palette clears WCAG contrast thresholds")]
    public void All_Palettes_Clear_Contrast_Thresholds()
    {
        var asm = typeof(ThemeDefinition).Assembly;
        var resourceNames = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("Vonvert.App.Themes.") && n.EndsWith(".json"))
            .ToList();
        Assert.NotEmpty(resourceNames);

        var failures = new List<string>();
        foreach (var rn in resourceNames)
        {
            using var stream = asm.GetManifestResourceStream(rn)!;
            using var reader = new StreamReader(stream);
            var theme = JsonConvert.DeserializeObject<ThemeDefinition>(reader.ReadToEnd());
            Assert.NotNull(theme);

            foreach (var (fg, bg, min, label) in Pairs)
            {
                double ratio = Contrast(fg(theme!), bg(theme));
                if (ratio < min)
                    failures.Add($"{theme!.Name} [{label}] = {ratio:F2}:1 (need {min}:1) fg={fg(theme)} bg={bg(theme)}");
            }
        }

        Assert.True(failures.Count == 0,
            $"Sub-threshold contrast pair(s) across palettes:\n{string.Join("\n", failures)}");
    }
}
