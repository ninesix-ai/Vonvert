// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

using Vonvert.Engine;
using Vonvert.Engine.Services;

namespace Vonvert.App.UIServices;

/// <summary>
/// A single theme expressed as flat color tokens. Every field maps 1:1 to a
/// brush / gradient key that the visual tree resolves through
/// <c>DynamicResource</c>, so the same set is what a theme JSON file and the
/// resource dictionary swap must agree on. Field defaults mirror the built-in
/// "Dark" theme, which is the palette <c>AppStyles/Theme.xaml</c> ships as its
/// fallback, so an all-defaults object reproduces the current on-screen look.
/// </summary>
public class ThemeDefinition
{
    public string Name { get; set; } = "Dark";
    public string DisplayName { get; set; } = "Dark";
    public string Author { get; set; } = "Vonvert";

    // Core surfaces
    public string Bg { get; set; } = "#FF12101E";
    public string Surface { get; set; } = "#FF191627";
    public string SurfaceLift { get; set; } = "#FF211D33";
    public string Border { get; set; } = "#FF2F2A48";
    public string Accent { get; set; } = "#FF8B5CF6";
    public string AccentHover { get; set; } = "#FFA78BFA";
    public string TextPrimary { get; set; } = "#FFF3F1FB";
    public string TextSub { get; set; } = "#FF8B87A8";
    public string Warning { get; set; } = "#FFFBBF24";

    // Layout surfaces
    public string TileSurface { get; set; } = "#FF1A1729";
    public string TileSelected { get; set; } = "#FF252041";
    public string PowerOn { get; set; } = "#FF34D399";
    public string PowerOff { get; set; } = "#FFFB7185";

    // Semantic washes (low-alpha tinted backgrounds; consumed as *Brush keys)
    public string WarnWash { get; set; } = "#1FFBBF24";
    public string OkWash { get; set; } = "#1F34D399";

    // Navigation icon hues
    public string IconVoices { get; set; } = "#FF22D3EE";
    public string IconRecord { get; set; } = "#FFFB7185";
    public string IconSettings { get; set; } = "#FF6366F1";
    public string IconAbout { get; set; } = "#FF34D399";
    public string IconSoundboard { get; set; } = "#FFFBBF24";

    // Soundboard pad-category hues
    public string IconCatDrums { get; set; } = "#FFCAA36E";
    public string IconCatTones { get; set; } = "#FF9E95D6";
    public string IconCatSFX { get; set; } = "#FF75B5D1";
    public string IconCatMemes { get; set; } = "#FFD28CB2";
    public string IconCatMusic { get; set; } = "#FF8EB88F";
    public string IconCatRetro { get; set; } = "#FF6AA3AE";
    public string IconCatAmbient { get; set; } = "#FFA1A8B1";

    // Control tokens (ComboBox, TextBox, etc.)
    public string CtrlBorder { get; set; } = "#FF37315A";
    public string CtrlSurface { get; set; } = "#FF1E1A30";
    public string CtrlText { get; set; } = "#FFD6D2E8";
    public string CtrlHover { get; set; } = "#FF282344";

    // Mini toggle tokens
    public string ToggleOffTrack { get; set; } = "#FF37315A";
    public string ToggleOffThumb { get; set; } = "#FF736C99";

    // Gradient endpoints
    public string AccentGradStart { get; set; } = "#FF8B5CF6";
    public string AccentGradEnd { get; set; } = "#FFEC4899";
    public string HeaderGradStart { get; set; } = "#FF191627";
    public string HeaderGradEnd { get; set; } = "#FF12101E";
    public string VUMeterGradStart { get; set; } = "#FF34D399";
    public string VUMeterGradMid { get; set; } = "#FFFBBF24";
    public string VUMeterGradEnd { get; set; } = "#FFFB7185";
    public string ComboSelectedGradStart { get; set; } = "#FF252041";
    public string ComboSelectedGradEnd { get; set; } = "#FF2B2550";
}

/// <summary>
/// Theme engine managing multiple themes and runtime theme switching.
///
/// Runtime switching works by rebuilding a fresh ResourceDictionary with all
/// themed brushes and appending it as the LAST entry of
/// Application.Current.Resources.MergedDictionaries. WPF searches merged
/// dictionaries in reverse insertion order (last added wins), so the appended
/// override outranks the Theme.xaml fallback that declares the same color
/// keys. WPF also seals the app-level dictionary after startup and
/// auto-freezes any Freezable written into it, so in-place color mutation of
/// the app dictionary can never propagate to already-resolved consumers — a
/// wholesale dictionary swap plus DynamicResource consumers is the only
/// reliable mechanism.
///
/// Persistence rides on the shared AppConfig singleton (ui.themeName), and
/// user-supplied themes live under the relocatable AppPaths.Root — nothing is
/// written to a fixed path, so a relocated data root and the uninstall purge
/// both keep working.
/// </summary>
public sealed class ThemeEngine : IDisposable
{
    // The built-in theme whose tokens equal the Theme.xaml fallback; used as the
    // default when no (or an unknown) preference is stored.
    public const string DefaultThemeName = "Dark";

    private static readonly Lazy<ThemeEngine> _lazy = new(() => new ThemeEngine());
    public static ThemeEngine Instance => _lazy.Value;

    private readonly Dictionary<string, ThemeDefinition> _themes = new();
    private ThemeDefinition? _currentTheme;
    private readonly string _themesFolder;
    private ResourceDictionary? _overrideDict;

    public IReadOnlyDictionary<string, ThemeDefinition> Themes => _themes;
    public ThemeDefinition? CurrentTheme => _currentTheme;
    public event Action? ThemeChanged;

    private ThemeEngine()
    {
        _themesFolder = Path.Combine(AppPaths.Root, "Themes");
        LoadBuiltInThemes();
        LoadCustomThemes();
    }

    /// <summary>Apply a theme by name and persist the choice.</summary>
    public void ApplyTheme(string themeName)
    {
        if (!_themes.TryGetValue(themeName, out var theme))
            return;

        _currentTheme = theme;
        ApplyThemeToResources(theme);
        SaveThemePreference(themeName);
        ThemeChanged?.Invoke();
    }

    /// <summary>Import a custom theme from a JSON file into the themes folder.</summary>
    public bool ImportTheme(string filePath)
    {
        try
        {
            var json = SafeFileHelper.ReadAllTextSafe(filePath);
            if (json == null) return false;
            var theme = JsonConvert.DeserializeObject<ThemeDefinition>(json);
            if (theme == null || string.IsNullOrEmpty(theme.Name)) return false;

            // Re-serialize the parsed object so only known ThemeDefinition fields
            // are written (strips any extra JSON keys from an arbitrary file).
            Directory.CreateDirectory(_themesFolder);
            string destPath = Path.Combine(_themesFolder, $"{SanitizeName(theme.Name)}.json");
            SafeFileHelper.WriteAllTextSafe(destPath, JsonConvert.SerializeObject(theme, Formatting.Indented));

            _themes[theme.Name] = theme;
            return true;
        }
        catch (Exception ex) { AppLog.Warning(ex, "ThemeEngine: ImportTheme failed"); return false; }
    }

    /// <summary>Export the current theme to a JSON file.</summary>
    public void ExportTheme(string filePath)
    {
        var json = JsonConvert.SerializeObject(_currentTheme, Formatting.Indented);
        SafeFileHelper.WriteAllTextSafe(filePath, json);
    }

    /// <summary>
    /// Apply the persisted theme choice at startup. Falls back to the built-in
    /// default when no preference is stored or the stored name is unknown.
    /// </summary>
    public void LoadSavedTheme()
    {
        try
        {
            var saved = AppConfig.Instance.Ui.ThemeName;
            if (!string.IsNullOrEmpty(saved) && _themes.ContainsKey(saved))
            {
                ApplyTheme(saved);
                return;
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "ThemeEngine: LoadSavedTheme failed"); }

        var fallback = _themes.ContainsKey(DefaultThemeName)
            ? DefaultThemeName
            : _themes.Keys.FirstOrDefault() ?? DefaultThemeName;
        ApplyTheme(fallback);
    }

    private void LoadBuiltInThemes()
    {
        // Built-in themes ship embedded in the assembly as
        // Vonvert.App.Themes.<filename>.json (see Vonvert.App.csproj).
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith("Vonvert.App.Themes.") || !resourceName.EndsWith(".json"))
                continue;

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var theme = JsonConvert.DeserializeObject<ThemeDefinition>(json);
                if (theme != null && !string.IsNullOrEmpty(theme.Name))
                {
                    _themes[theme.Name] = theme;
                }
            }
            catch { /* skip malformed theme resources */ }
        }
    }

    private void LoadCustomThemes()
    {
        if (!Directory.Exists(_themesFolder)) return;

        try
        {
            foreach (var file in Directory.GetFiles(_themesFolder, "*.json"))
            {
                var json = SafeFileHelper.ReadAllTextSafe(file);
                if (json == null) continue;
                var theme = JsonConvert.DeserializeObject<ThemeDefinition>(json);
                if (theme != null && !_themes.ContainsKey(theme.Name))
                {
                    _themes[theme.Name] = theme;
                }
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "ThemeEngine: LoadCustomThemes failed"); }
    }

    private void ApplyThemeToResources(ThemeDefinition theme)
    {
        var resources = Application.Current.Resources;
        var fresh = BuildThemeDictionary(theme);

        var merged = resources.MergedDictionaries;
        if (_overrideDict != null && merged.Contains(_overrideDict))
        {
            merged[merged.IndexOf(_overrideDict)] = fresh;
        }
        else
        {
            // Append LAST: WPF searches merged dictionaries in reverse
            // insertion order, so the last entry outranks Theme.xaml's
            // same-key fallback brushes.
            merged.Add(fresh);
        }
        _overrideDict = fresh;
    }

    /// <summary>
    /// Build a fresh ResourceDictionary containing every themed brush and
    /// gradient for the given theme. Values are unfrozen so the dictionary
    /// itself can be swapped freely on the next apply. Keys here must match the
    /// brush keys declared in AppStyles/Theme.xaml so the override wins.
    /// </summary>
    public static ResourceDictionary BuildThemeDictionary(ThemeDefinition t)
    {
        var d = new ResourceDictionary();

        void Brush(string key, string hex)
            => d[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        void Grad2(string key, string start, string end, Point sp, Point ep)
        {
            var b = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString(start),
                (Color)ColorConverter.ConvertFromString(end), sp, ep);
            d[key] = b;
        }

        // Core surfaces
        Brush("Bg", t.Bg);
        Brush("Surface", t.Surface);
        Brush("SurfaceLift", t.SurfaceLift);
        Brush("Border", t.Border);
        Brush("Accent", t.Accent);
        Brush("AccentHover", t.AccentHover);
        Brush("TextPrimary", t.TextPrimary);
        Brush("TextSub", t.TextSub);
        Brush("Warning", t.Warning);

        // Layout
        Brush("TileSurface", t.TileSurface);
        Brush("TileSelected", t.TileSelected);
        Brush("PowerOn", t.PowerOn);
        Brush("PowerOff", t.PowerOff);

        // Semantic washes (consumed as *Brush keys in Theme.xaml)
        Brush("WarnWashBrush", t.WarnWash);
        Brush("OkWashBrush", t.OkWash);

        // Navigation icons
        Brush("IconVoices", t.IconVoices);
        Brush("IconRecord", t.IconRecord);
        Brush("IconSettings", t.IconSettings);
        Brush("IconAbout", t.IconAbout);
        Brush("IconSoundboard", t.IconSoundboard);

        // Soundboard pad-category hues
        Brush("IconCatDrums", t.IconCatDrums);
        Brush("IconCatTones", t.IconCatTones);
        Brush("IconCatSFX", t.IconCatSFX);
        Brush("IconCatMemes", t.IconCatMemes);
        Brush("IconCatMusic", t.IconCatMusic);
        Brush("IconCatRetro", t.IconCatRetro);
        Brush("IconCatAmbient", t.IconCatAmbient);

        // Controls
        Brush("CtrlBorder", t.CtrlBorder);
        Brush("CtrlSurface", t.CtrlSurface);
        Brush("CtrlText", t.CtrlText);
        Brush("CtrlHover", t.CtrlHover);

        // Mini toggle
        Brush("ToggleOffTrack", t.ToggleOffTrack);
        Brush("ToggleOffThumb", t.ToggleOffThumb);

        // Gradients (diagonal brand, vertical header, horizontal combo / VU meter)
        Grad2("AccentGrad", t.AccentGradStart, t.AccentGradEnd, new Point(0, 0), new Point(1, 1));
        Grad2("HeaderGrad", t.HeaderGradStart, t.HeaderGradEnd, new Point(0, 0), new Point(0, 1));
        Grad2("ComboSelectedGrad", t.ComboSelectedGradStart, t.ComboSelectedGradEnd, new Point(0, 0), new Point(1, 0));

        var vu = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        vu.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(t.VUMeterGradStart), 0));
        vu.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(t.VUMeterGradMid), 0.7));
        vu.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(t.VUMeterGradEnd), 1));
        d["VUMeterGrad"] = vu;

        return d;
    }

    private static void SaveThemePreference(string themeName)
    {
        AppConfig.Instance.Ui.ThemeName = themeName;
        AppConfig.Instance.Save();
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    public void Dispose()
    {
    }
}
