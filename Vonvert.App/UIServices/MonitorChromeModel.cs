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
/// The shape of the canvas the window is being captured into. 16:9 is the desktop default,
/// but a Shorts/Reels creator works in 9:16 and an overlay on a square feed wants 1:1, and
/// until now both of them had to crop the capture by hand in OBS.
/// </summary>
public enum MonitorCanvasShape { Widescreen, Vertical, Square }

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

    /// <summary>Which canvas the capture is going into. Widescreen is the shipped shape.</summary>
    public MonitorCanvasShape Shape { get; private set; } = MonitorCanvasShape.Widescreen;

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

    /// <summary>Widescreen -> vertical -> square -> widescreen.</summary>
    public void CycleShape() => SetShape(Shape switch
    {
        MonitorCanvasShape.Widescreen => MonitorCanvasShape.Vertical,
        MonitorCanvasShape.Vertical   => MonitorCanvasShape.Square,
        _                             => MonitorCanvasShape.Widescreen,
    });

    /// <summary>
    /// Choose a canvas shape. Leaving "the whole work area" is part of choosing one: Fill
    /// ignores the shape by definition, so a button that changed nothing visible would be a
    /// control that lies about what it did.
    /// </summary>
    public void SetShape(MonitorCanvasShape shape)
    {
        Shape = shape;
        if (SizeMode == MonitorSizeMode.FillWorkArea) SizeMode = MonitorSizeMode.Native;
    }

    /// <summary>Return the window to the shipped defaults (topmost, native size, 16:9).</summary>
    public void ResetToDefault()
    {
        Topmost  = true;
        SizeMode = MonitorSizeMode.Native;
        Shape    = MonitorCanvasShape.Widescreen;
    }

    /// <summary>
    /// The smallest window that still carries the layout, per shape. The shipped 640x360 floor
    /// is a statement about the landscape arrangement; applied to a 9:16 window on a 1080p
    /// desktop it would force 640 wide against the 608 the ratio wants and quietly break the
    /// shape that was just chosen. Every minimum keeps its own ratio.
    /// </summary>
    public static (int Width, int Height) MinimumFor(MonitorCanvasShape shape) => shape switch
    {
        MonitorCanvasShape.Vertical => (360, 640),
        MonitorCanvasShape.Square   => (480, 480),
        _                           => (MinWidth, MinHeight),
    };

    private static (int Width, int Height) TargetFor(MonitorCanvasShape shape, MonitorSizeMode size)
    {
        bool large = size == MonitorSizeMode.Large;
        return shape switch
        {
            MonitorCanvasShape.Vertical => large ? (1080, 1920) : (720, 1280),
            MonitorCanvasShape.Square   => large ? (1200, 1200) : (900, 900),
            _                           => large ? (LargeWidth, LargeHeight) : (NativeWidth, NativeHeight),
        };
    }

    /// <summary>
    /// Resolve the current size mode against a work area (device-independent pixels), keeping
    /// the chosen shape's ratio. A degenerate work area (no screen information yet) falls back
    /// to that shape's minimum rather than returning a zero-size window.
    /// </summary>
    public (int Width, int Height) Resolve(int workWidth, int workHeight)
    {
        var minimum = MinimumFor(Shape);
        if (workWidth <= 0 || workHeight <= 0)
            return minimum;

        if (SizeMode == MonitorSizeMode.FillWorkArea)
            return (workWidth, workHeight);

        var (targetW, targetH) = TargetFor(Shape, SizeMode);

        int w = Math.Min(targetW, workWidth);
        int h = (int)Math.Round((double)w * targetH / targetW);
        if (h > workHeight)                 // which edge binds depends on the shape, so try both
        {
            h = workHeight;
            w = (int)Math.Round((double)h * targetW / targetH);
        }
        return w < minimum.Width || h < minimum.Height ? minimum : (w, h);
    }
}
