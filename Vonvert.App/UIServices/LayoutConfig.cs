// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vonvert.Engine;
using WCornerRadius = System.Windows.CornerRadius;

namespace Vonvert.App.UIServices;

/// <summary>
/// Loads UI layout parameters from a JSON file and injects them as WPF resources.
/// Users can edit %APPDATA%\Vonvert\layout.json to customise preset tile sizes
/// without recompiling.  Missing keys fall back to built-in defaults.
/// </summary>
public sealed class LayoutConfig
{
    // ── Built-in defaults ───────────────────────────────────────────────
    private static readonly JObject Defaults = JObject.Parse(@"{
        ""presetTile.width""        : 64,
        ""presetTile.height""       : 60,
        ""presetTile.margin""       : 8,
        ""presetTile.iconSize""     : 20,
        ""presetTile.fontSize""     : 10,
        ""presetTile.maxTextWidth"" : 56,
        ""presetTile.cornerRadius"" : 10
    }");

    private readonly JObject _values;

    // ── Singleton ───────────────────────────────────────────────────────
    private static readonly Lazy<LayoutConfig> _lazy = new(() => new LayoutConfig());
    public static LayoutConfig Instance => _lazy.Value;

    private LayoutConfig()
    {
        _values = (JObject)Defaults.DeepClone();
        LoadUserOverrides();
    }

    /// <summary>Internal constructor for unit testing with controlled JSON values.</summary>
    internal LayoutConfig(JObject testJson)
    {
        _values = (JObject)testJson.DeepClone();
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Inject all layout values into <see cref="Application.Current"/>.Resources
    /// so that XAML <c>{DynamicResource key}</c> can pick them up.
    /// Call once during <c>OnStartup</c>, before <c>MainWindow</c> is created.
    /// </summary>
    // Layout keys whose numeric values represent corner radii.
    // They must be stored as CornerRadius structs (not double) so that
    // {DynamicResource …} binding to Border.CornerRadius works correctly.
    private static readonly HashSet<string> CornerRadiusKeys = new(StringComparer.Ordinal)
    {
        "presetTile.cornerRadius",
    };

    // Layout keys whose numeric values represent uniform margins.
    // They must be stored as Thickness structs (not double) so that
    // {DynamicResource …} binding to FrameworkElement.Margin works correctly.
    private static readonly HashSet<string> MarginKeys = new(StringComparer.Ordinal)
    {
        "presetTile.margin",
    };

    public void ApplyToResources()
    {
        var map = BuildResourceMap();
        var res = Application.Current.Resources;
        foreach (var (key, val) in map)
            res[key] = val;
    }

    /// <summary>
    /// Convert the internal JSON values into a dictionary of WPF-typed objects.
    /// Extracted for testability — the type mapping is the critical logic that
    /// must produce Thickness/CornerRadius (not double) for the right keys.
    /// </summary>
    internal Dictionary<string, object> BuildResourceMap()
    {
        var map = new Dictionary<string, object>();
        foreach (var prop in _values.Properties())
        {
            var key = prop.Name;
            var val = prop.Value;

            object? wpfValue = val.Type switch
            {
                JTokenType.Integer  => CornerRadiusKeys.Contains(key)
                    ? (object)new WCornerRadius((double)val.Value<int>())
                    : MarginKeys.Contains(key)
                        ? (object)new Thickness((double)val.Value<int>())
                        : (double)val.Value<int>(),
                JTokenType.Float    => MarginKeys.Contains(key)
                    ? (object)new Thickness(val.Value<double>())
                    : (object)val.Value<double>(),
                JTokenType.String   => ParseThickness(val.Value<string>()!),
                JTokenType.Boolean  => val.Value<bool>(),
                _                   => null
            };

            if (wpfValue != null)
                map[key] = wpfValue;
        }
        return map;
    }

    /// <summary>Get a double value (with default fallback).</summary>
    public double GetDouble(string key, double fallback = 0)
        => _values[key]?.Value<double>() ?? fallback;

    /// <summary>Get a Thickness value (with default fallback).</summary>
    public Thickness GetThickness(string key, Thickness fallback = default)
    {
        var raw = _values[key]?.Value<string>();
        return raw != null ? ParseThickness(raw) : fallback;
    }

    // ── Loading ─────────────────────────────────────────────────────────

    private void LoadUserOverrides()
    {
        try
        {
            string path = ConfigPath;
            var json = Vonvert.Engine.SafeFileHelper.ReadAllTextSafe(path);
            if (json == null) return;

            var user = JObject.Parse(json);
            foreach (var prop in user.Properties())
            {
                _values[prop.Name] = prop.Value;
            }

            AppLog.Information("LayoutConfig: loaded user overrides from {Path}", path);
        }
        catch (Exception ex)
        {
            AppLog.Warning("LayoutConfig: failed to load overrides — {Msg}", ex.Message);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>{DataRoot}\layout.json</summary>
    public static string ConfigPath => Path.Combine(
        AppPaths.Root, "layout.json");

    /// <summary>
    /// Parse a thickness string ("6" or "6,4" or "6,4,8,2") into a WPF Thickness.
    /// </summary>
    internal static Thickness ParseThickness(string s)
    {
        var parts = s.Split(',');
        return parts.Length switch
        {
            1 => new Thickness(double.Parse(parts[0])),
            2 => new Thickness(double.Parse(parts[0]), double.Parse(parts[1]),
                               double.Parse(parts[0]), double.Parse(parts[1])),
            4 => new Thickness(double.Parse(parts[0]), double.Parse(parts[1]),
                               double.Parse(parts[2]), double.Parse(parts[3])),
            _ => default
        };
    }
}
