// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using Newtonsoft.Json;
using Vonvert.App.UIServices;
using Vonvert.Engine;
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.UIServices;

/// <summary>
/// Guards the structural invariants of the theme system: the token vocabulary a
/// theme swap relies on, the well-formedness of the shipped built-in palettes, and
/// the zero-regression promise that the default theme equals the Theme.xaml fallback.
/// Reads only embedded resources and the AppConfig seam, so it needs no WPF
/// Application instance and runs in the headless test host.
/// </summary>
[Collection("AppPathsSeam")]
public sealed class ThemeEngineTests : IDisposable
{
    private readonly string _tempDir;

    public ThemeEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_Theme_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        AppPaths.RootOverride = _tempDir;
        AppPaths.Invalidate();
    }

    public void Dispose()
    {
        // Leave the shared AppConfig singleton in a neutral state for other suites.
        AppConfig.Instance.Ui.ThemeName = ThemeEngine.DefaultThemeName;
        AppPaths.RootOverride = null;
        AppPaths.PointerDirOverride = null;
        AppPaths.Invalidate();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // Every themeable brush / gradient key in AppStyles/Theme.xaml, expressed as the
    // ThemeDefinition field that feeds it. A theme swap silently leaves a control on
    // its fallback color if any of these is missing — this list is the contract.
    private static readonly string[] XamlTokenFields =
    {
        nameof(ThemeDefinition.Bg), nameof(ThemeDefinition.Surface), nameof(ThemeDefinition.SurfaceLift),
        nameof(ThemeDefinition.Border), nameof(ThemeDefinition.Accent), nameof(ThemeDefinition.AccentHover), nameof(ThemeDefinition.TextPrimary),
        nameof(ThemeDefinition.TextSub), nameof(ThemeDefinition.Warning), nameof(ThemeDefinition.TileSurface),
        nameof(ThemeDefinition.TileSelected), nameof(ThemeDefinition.PowerOn), nameof(ThemeDefinition.PowerOff),
        nameof(ThemeDefinition.WarnWash), nameof(ThemeDefinition.OkWash), nameof(ThemeDefinition.IconVoices),
        nameof(ThemeDefinition.IconRecord), nameof(ThemeDefinition.IconSettings), nameof(ThemeDefinition.IconAbout),
        nameof(ThemeDefinition.IconSoundboard), nameof(ThemeDefinition.IconCatDrums), nameof(ThemeDefinition.IconCatTones),
        nameof(ThemeDefinition.IconCatSFX), nameof(ThemeDefinition.IconCatMemes), nameof(ThemeDefinition.IconCatMusic),
        nameof(ThemeDefinition.IconCatRetro), nameof(ThemeDefinition.IconCatAmbient), nameof(ThemeDefinition.CtrlBorder),
        nameof(ThemeDefinition.CtrlSurface), nameof(ThemeDefinition.CtrlText), nameof(ThemeDefinition.CtrlHover),
        nameof(ThemeDefinition.ToggleOffTrack), nameof(ThemeDefinition.ToggleOffThumb),
        nameof(ThemeDefinition.AccentGradStart), nameof(ThemeDefinition.AccentGradEnd),
        nameof(ThemeDefinition.HeaderGradStart), nameof(ThemeDefinition.HeaderGradEnd),
        nameof(ThemeDefinition.VUMeterGradStart), nameof(ThemeDefinition.VUMeterGradMid), nameof(ThemeDefinition.VUMeterGradEnd),
        nameof(ThemeDefinition.ComboSelectedGradStart), nameof(ThemeDefinition.ComboSelectedGradEnd),
    };

    private static ThemeDefinition LoadBuiltIn(string name)
    {
        var asm = typeof(ThemeDefinition).Assembly;
        var rn = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n == "Vonvert.App.Themes." + name + ".json");
        Assert.NotNull(rn);
        using var stream = asm.GetManifestResourceStream(rn!)!;
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<ThemeDefinition>(reader.ReadToEnd())!;
    }

    [Fact(DisplayName = "THM-001: ThemeDefinition exposes a field for every Theme.xaml token")]
    public void ThemeDefinition_Covers_All_Xaml_Tokens()
    {
        var props = typeof(ThemeDefinition).GetProperties().Select(p => p.Name).ToHashSet();
        foreach (var field in XamlTokenFields)
            Assert.Contains(field, props);
    }

    [Fact(DisplayName = "THM-002: built-in themes are embedded and every colour token is a legal #AARRGGBB")]
    public void BuiltInThemes_AreEmbeddedAndWellFormed()
    {
        var asm = typeof(ThemeDefinition).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("Vonvert.App.Themes.") && n.EndsWith(".json"))
            .ToList();
        Assert.True(names.Count >= 10, $"expected >= 10 embedded themes, found {names.Count}");

        var colourProps = typeof(ThemeDefinition).GetProperties()
            .Where(p => p.PropertyType == typeof(string)
                        && p.Name != nameof(ThemeDefinition.Name)
                        && p.Name != nameof(ThemeDefinition.DisplayName)
                        && p.Name != nameof(ThemeDefinition.Author))
            .ToList();

        foreach (var rn in names)
        {
            using var stream = asm.GetManifestResourceStream(rn)!;
            using var reader = new StreamReader(stream);
            var theme = JsonConvert.DeserializeObject<ThemeDefinition>(reader.ReadToEnd());
            Assert.NotNull(theme);
            Assert.False(string.IsNullOrWhiteSpace(theme!.Name));
            foreach (var p in colourProps)
            {
                var value = (string?)p.GetValue(theme);
                Assert.False(string.IsNullOrWhiteSpace(value), $"{theme.Name}.{p.Name} is empty");
                Assert.Matches("^#[0-9A-Fa-f]{8}$", value!);
            }
        }
    }

    [Fact(DisplayName = "THM-003: the default 'Dark' theme equals the shipped Theme.xaml fallback (no first-launch drift)")]
    public void Dark_Matches_Xaml_Fallback()
    {
        var dark = LoadBuiltIn("Dark");
        Assert.Equal("#FF12101E", dark.Bg);
        Assert.Equal("#FF191627", dark.Surface);
        Assert.Equal("#FF8B5CF6", dark.Accent);
        Assert.Equal("#FFF3F1FB", dark.TextPrimary);
        Assert.Equal("#FF8B87A8", dark.TextSub);
        Assert.Equal("#FF6366F1", dark.IconSettings);
        Assert.Equal("#1FFBBF24", dark.WarnWash);
        Assert.Equal("#1F34D399", dark.OkWash);
        Assert.Equal("#FFCAA36E", dark.IconCatDrums);
    }

    [Fact(DisplayName = "THM-004: the selected theme name round-trips through AppConfig")]
    public void ThemeName_RoundTrips()
    {
        var cfg = AppConfig.Instance;
        cfg.Ui.ThemeName = "Neon";
        cfg.Save();

        cfg.Ui.ThemeName = "Tampered";
        cfg.Load();
        Assert.Equal("Neon", cfg.Ui.ThemeName);
    }
}
