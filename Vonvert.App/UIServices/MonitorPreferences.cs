// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Vonvert.Engine;

namespace Vonvert.App.UIServices;

/// <summary>A work area in device-independent units, passed in so the maths is testable.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

/// <summary>
/// Keeps a restored window where the user left it, and rescues it when that place no
/// longer exists.
///
/// Saving a position has a standard failure: somebody docks a laptop, arranges the monitor
/// into a corner, undocks it, and the borderless window - which has no title bar to grab -
/// comes back off-screen and looks like the feature is broken. An unreachable position is
/// therefore re-centred on the primary screen instead of clamped into a corner.
/// </summary>
public static class MonitorPlacement
{
    public static (double Left, double Top) Clamp(
        double left, double top, double width, double height, IReadOnlyList<ScreenRect> screens)
    {
        // Nothing reported (headless service run, screen enumeration failed): keep what we
        // were asked for rather than inventing a position.
        if (screens is null || screens.Count == 0) return (left, top);

        if (IsFullyOn(left, top, width, height, screens)) return (left, top);

        var primary = screens[0];
        return (primary.X + (primary.Width - width) / 2,
                primary.Y + (primary.Height - height) / 2);
    }

    private static bool IsFullyOn(double left, double top, double width, double height,
                                  IReadOnlyList<ScreenRect> screens)
    {
        foreach (var s in screens)
        {
            if (left >= s.X && top >= s.Y
                && left + width <= s.X + s.Width
                && top + height <= s.Y + s.Height)
                return true;
        }
        return false;
    }
}

/// <summary>
/// How the user last set the monitor up. Size and position in device-independent units,
/// plus the three choices that are re-made every session: which voice to look at, the
/// loudness target, and whether the labels speak plain words.
/// </summary>
public sealed class MonitorPreferences
{
    /// <summary>False until the user has actually moved or resized the window, so a first
    /// run keeps the XAML default of a centred 1280x720.</summary>
    public bool HasBounds { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = MonitorChromeModel.NativeWidth;
    public double Height { get; set; } = MonitorChromeModel.NativeHeight;
    public bool ShowDry { get; set; }
    public bool Topmost { get; set; } = true;
    public LufsTargetPreset Target { get; set; } = LufsTargetPreset.Broadcast;
    public MonitorLabelMode LabelMode { get; set; } = MonitorLabelMode.Plain;
}

/// <summary>Reads and writes <see cref="MonitorPreferences"/>. Never throws: a missing,
/// half-written or future-versioned file means defaults, not a window that will not open.</summary>
public static class MonitorStore
{
    private sealed class Dto
    {
        public bool HasBounds { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool ShowDry { get; set; }
        public bool Topmost { get; set; } = true;
        public int Target { get; set; }
        public int LabelMode { get; set; }
    }

    public static string FilePath => Path.Combine(AppPaths.Root, "monitor.json");

    /// <summary>The file is written camelCase and read case-insensitively, so a hand-edit
    /// or a differently-cased older file still loads instead of silently becoming defaults.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static MonitorPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new MonitorPreferences();
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(FilePath), JsonOptions);
            if (dto is null) return new MonitorPreferences();

            return new MonitorPreferences
            {
                HasBounds = dto.HasBounds,
                Left = dto.Left,
                Top = dto.Top,
                Width = dto.Width > 0 ? dto.Width : MonitorChromeModel.NativeWidth,
                Height = dto.Height > 0 ? dto.Height : MonitorChromeModel.NativeHeight,
                ShowDry = dto.ShowDry,
                Topmost = dto.Topmost,
                // An unknown number is from a file written by another version; fall back to
                // the shipped default instead of casting into an undefined enum value.
                Target = Enum.IsDefined(typeof(LufsTargetPreset), dto.Target)
                    ? (LufsTargetPreset)dto.Target : LufsTargetPreset.Broadcast,
                LabelMode = Enum.IsDefined(typeof(MonitorLabelMode), dto.LabelMode)
                    ? (MonitorLabelMode)dto.LabelMode : MonitorLabelMode.Plain,
            };
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[MonitorStore] could not read monitor.json; using defaults");
            return new MonitorPreferences();
        }
    }

    public static void Save(MonitorPreferences prefs)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var dto = new Dto
            {
                HasBounds = prefs.HasBounds,
                Left = prefs.Left, Top = prefs.Top,
                Width = prefs.Width, Height = prefs.Height,
                ShowDry = prefs.ShowDry, Topmost = prefs.Topmost,
                Target = (int)prefs.Target, LabelMode = (int)prefs.LabelMode,
            };
            // Write-then-move so a crash mid-save cannot leave a truncated file behind.
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(dto, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[MonitorStore] could not write monitor.json");
        }
    }
}
