// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// CH-001 ~ CH-015: the monitor's chrome state (always-on-top, window size cycle, canvas shape).
// Pure and WPF-free on purpose: the hover toolbar is a thin view over this.
public sealed class MonitorChromeModelTests
{
    [Fact(DisplayName = "CH-001: initial chrome state is topmost + native size, resolved inside a 1080p desktop")]
    public void CH001_InitialNative()
    {
        var c = new MonitorChromeModel();
        Assert.True(c.Topmost);
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);
        Assert.Equal((1280, 720), c.Resolve(1920, 1080));
    }

    [Fact(DisplayName = "CH-002: toggling topmost flips it both ways and never touches the size")]
    public void CH002_TopmostToggle()
    {
        var c = new MonitorChromeModel();
        c.ToggleTopmost();
        Assert.False(c.Topmost);
        Assert.Equal((1280, 720), c.Resolve(1920, 1080));   // size untouched
        c.ToggleTopmost();
        Assert.True(c.Topmost);
    }

    [Fact(DisplayName = "CH-003: the size button cycles Native -> Large -> Fill -> Native")]
    public void CH003_SizeCycleIsDeterministic()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.Large, c.SizeMode);
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.FillWorkArea, c.SizeMode);
        c.CycleSize();
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);   // wraps, never a dead end
    }

    [Fact(DisplayName = "CH-004: Large shrinks to the work-area width and keeps 16:9")]
    public void CH004_LargeFitsNarrowDesktop()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();                                        // -> Large (1920x1080 wanted)
        Assert.Equal((1024, 576), c.Resolve(1024, 640));
    }

    [Fact(DisplayName = "CH-005: a short desktop limits the height instead, and never overflows the work area")]
    public void CH005_HeightLimited()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();                                        // Native -> Large (1920x1080 wanted)
        (int w, int h) = c.Resolve(1600, 700);
        Assert.Equal(700, h);                                 // height is the binding limit
        Assert.Equal(1244, w);                                // 700 * 16 / 9, floored
        Assert.True(w <= 1600 && h <= 700, "resolved size must stay inside the work area");
    }

    [Fact(DisplayName = "CH-006: Fill covers the whole work area, not more")]
    public void CH006_FillWorkArea()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();
        c.CycleSize();
        Assert.Equal((2560, 1440), c.Resolve(2560, 1440));
    }

    [Fact(DisplayName = "CH-007: a degenerate work area falls back to the minimum instead of a zero-size window")]
    public void CH007_DegenerateWorkArea()
    {
        Assert.Equal((640, 360), new MonitorChromeModel().Resolve(0, 0));
        Assert.Equal((640, 360), new MonitorChromeModel().Resolve(-1920, -1080));
    }

    [Fact(DisplayName = "CH-008: restoring the default view returns to topmost + native from any state")]
    public void CH008_ResetToDefault()
    {
        var c = new MonitorChromeModel();
        c.ToggleTopmost();
        c.CycleSize();
        c.CycleSize();
        c.ResetToDefault();
        Assert.True(c.Topmost);
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);
    }

    // CH-009 ~ CH-014: canvas shape. The window used to be 16:9 and nothing else, which
    // leaves a Shorts/Reels creator (9:16) or a square overlay (1:1) with a capture that has
    // to be cropped by hand in OBS.
    [Fact(DisplayName = "CH-009: the widescreen shape reproduces the shipped numbers exactly")]
    public void CH009_WidescreenIsTheShippedShape()
    {
        var c = new MonitorChromeModel();
        Assert.Equal(MonitorCanvasShape.Widescreen, c.Shape);
        Assert.Equal((1280, 720), c.Resolve(1920, 1080));
        c.CycleSize();
        Assert.Equal((1244, 700), c.Resolve(1600, 700));
    }

    [Fact(DisplayName = "CH-010: the vertical shape fills a 1080x1920 canvas exactly, and stays 9:16 on a wide one")]
    public void CH010_VerticalFitsAPortraitCanvas()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();                            // -> Large
        c.SetShape(MonitorCanvasShape.Vertical);
        Assert.Equal((1080, 1920), c.Resolve(1080, 1920));    // the acceptance case: no crop needed in OBS

        var wide = new MonitorChromeModel();
        wide.SetShape(MonitorCanvasShape.Vertical);
        (int w, int h) = wide.Resolve(1920, 1080);            // on a landscape desktop height binds
        Assert.Equal(1080, h);
        Assert.True(w * 16 <= h * 9 + 16 && w * 16 >= h * 9 - 16,
            $"9:16 expected, got {w}x{h}");
    }

    [Fact(DisplayName = "CH-011: the square shape keeps 1:1 and never overflows the work area")]
    public void CH011_SquareStaysSquare()
    {
        var c = new MonitorChromeModel();
        c.SetShape(MonitorCanvasShape.Square);
        Assert.Equal((900, 900), c.Resolve(1920, 1080));      // native square target fits with room to spare

        (int w, int h) = c.Resolve(700, 1000);                // narrow desktop: width binds, still square
        Assert.Equal(h, w);
        Assert.True(w <= 700 && h <= 1000, $"{w}x{h} escapes a 700x1000 work area");
    }

    [Fact(DisplayName = "CH-012: the minimum size follows the shape, or a portrait window cannot keep its ratio")]
    public void CH012_MinimumFollowsTheShape()
    {
        // The shipped 640x360 floor is a statement about the landscape layout. Applied to a
        // 9:16 window on a 1080p desktop it would force 640 wide against 608 and quietly break
        // the shape the user just asked for.
        Assert.Equal((640, 360), MonitorChromeModel.MinimumFor(MonitorCanvasShape.Widescreen));
        Assert.Equal((360, 640), MonitorChromeModel.MinimumFor(MonitorCanvasShape.Vertical));
        Assert.Equal((480, 480), MonitorChromeModel.MinimumFor(MonitorCanvasShape.Square));
    }

    [Fact(DisplayName = "CH-013: choosing a shape while Fill is on leaves Fill, so the choice is visible")]
    public void CH013_ShapeEndsFill()
    {
        var c = new MonitorChromeModel();
        c.CycleSize();
        c.CycleSize();                                        // -> FillWorkArea
        c.SetShape(MonitorCanvasShape.Vertical);
        Assert.Equal(MonitorSizeMode.Native, c.SizeMode);
        Assert.NotEqual((1920, 1080), c.Resolve(1920, 1080));  // it did not stay "the whole screen"
    }

    [Fact(DisplayName = "CH-014: cycling shapes visits all three, wraps, and the default view returns to widescreen")]
    public void CH014_ShapeCycleAndReset()
    {
        var c = new MonitorChromeModel();
        var seen = new System.Collections.Generic.List<MonitorCanvasShape>();
        for (int i = 0; i < 3; i++) { c.CycleShape(); seen.Add(c.Shape); }
        Assert.Equal(3, seen.Distinct().Count());
        Assert.Equal(MonitorCanvasShape.Widescreen, c.Shape);

        c.SetShape(MonitorCanvasShape.Square);
        c.ResetToDefault();
        Assert.Equal(MonitorCanvasShape.Widescreen, c.Shape);
    }

    [Theory(DisplayName = "CH-015: every language names the three shapes distinctly and keeps the value slot")]
    [InlineData("en")] [InlineData("zh")] [InlineData("de")] [InlineData("fr")]
    [InlineData("es")] [InlineData("pt-BR")] [InlineData("ru")] [InlineData("it")]
    [InlineData("pl")] [InlineData("tr")] [InlineData("ja")] [InlineData("ko")]
    public void CH015_ShapeCopyExistsEverywhere(string lang)
    {
        var ui = ReadUiSection(lang);
        var names = new[]
        {
            LocalizationManager.ShapeKeyFor(MonitorCanvasShape.Widescreen),
            LocalizationManager.ShapeKeyFor(MonitorCanvasShape.Vertical),
            LocalizationManager.ShapeKeyFor(MonitorCanvasShape.Square),
        };
        Assert.Equal(3, names.Distinct().Count());
        var missing = names.Append("FsShape").Where(k => !ui.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"{lang}: missing shape copy [{string.Join(", ", missing)}]");

        // The button reads "Canvas: Widescreen (16:9)"; without the slot it would print the
        // heading alone and report no state at all.
        Assert.Contains("{0}", ui["FsShape"]);
        // A shape nobody can tell apart from another is a button that does nothing visible.
        Assert.Equal(3, names.Select(k => ui[k]).Distinct().Count());
    }

    private static System.Collections.Generic.Dictionary<string, string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = System.Text.Json.JsonDocument.Parse(stream);
        var map = new System.Collections.Generic.Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }
}
