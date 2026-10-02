// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>
/// Window sizes the monitor's size button cycles through.
/// <see cref="FillWorkArea"/> means "the whole work area", not
/// <c>WindowState.Maximized</c>: a borderless window maximized the WPF way covers
/// the taskbar, which is exactly what traps a novice who then cannot reach anything else.
/// </summary>
public enum MonitorSizeMode { Native, Large, FillWorkArea }

/// <summary>
/// Pure state behind the monitor's hover toolbar: always-on-top and the window-size
/// cycle. Deliberately WPF-free (no <c>Window</c>, no <c>SystemParameters</c>) so the
/// size arithmetic and the cycle order are unit-testable without a dispatcher —
/// same reasoning as <see cref="PaneLayoutModel"/>.
/// </summary>
public sealed class MonitorChromeModel
{
    // Mirror of the XAML defaults: Width/Height 1280x720, MinWidth/MinHeight 640x360.
    public const int NativeWidth  = 1280;
    public const int NativeHeight = 720;
    public const int LargeWidth   = 1920;
    public const int LargeHeight  = 1080;
    public const int MinWidth     = 640;
    public const int MinHeight    = 360;

    public MonitorChromeModel() => ResetToDefault();

    public bool Topmost { get; private set; }
    public MonitorSizeMode SizeMode { get; private set; }

    public void ToggleTopmost() => Topmost = !Topmost;

    /// <summary>Advance Native -> Large -> FillWorkArea -> Native. Never a dead end,
    /// so the user can always get back to the default by pressing the same button.</summary>
    public void CycleSize()
        => SizeMode = SizeMode switch
        {
            MonitorSizeMode.Native        => MonitorSizeMode.Large,
            MonitorSizeMode.Large         => MonitorSizeMode.FillWorkArea,
            _                             => MonitorSizeMode.Native,
        };

    /// <summary>Return the window to the shipped defaults (topmost, native size).</summary>
    public void ResetToDefault()
    {
        Topmost  = true;
        SizeMode = MonitorSizeMode.Native;
    }

    /// <summary>
    /// Resolve the current size mode against a work area (device-independent pixels),
    /// always staying inside it while keeping the 16:9 shape the OBS canvas expects.
    /// A degenerate work area (no screen information yet) falls back to the minimum
    /// rather than returning a zero-size window.
    /// </summary>
    public (int Width, int Height) Resolve(int workWidth, int workHeight)
    {
        if (workWidth <= 0 || workHeight <= 0)
            return (MinWidth, MinHeight);

        if (SizeMode == MonitorSizeMode.FillWorkArea)
            return (workWidth, workHeight);

        int targetW = SizeMode == MonitorSizeMode.Large ? LargeWidth : NativeWidth;

        int w = Math.Min(targetW, workWidth);
        int h = w * 9 / 16;
        if (h > workHeight)                 // short desktop: height binds, re-derive width
        {
            h = workHeight;
            w = h * 16 / 9;
        }
        return (w, h);
    }
}
