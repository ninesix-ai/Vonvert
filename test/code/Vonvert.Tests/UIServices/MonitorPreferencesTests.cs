// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using Xunit;
using Vonvert.App.UIServices;
using Vonvert.Engine;

namespace Vonvert.Tests.UIServices;

// PR-001 ~ PR-004, RB-001 ~ RB-004: remembering how the user set the monitor up.
//
// Every streamer who positions this window for OBS - size, corner, which voice to watch,
// which loudness target - had to do it again next session, because the window always came
// back 1280x720, centred, wet, broadcast. Storing a position has its own classic bug: the
// second monitor disappears and the window comes back off-screen with no title bar to drag.
// Both halves are pinned here.
//
// Joined to the AppPathsSeam collection like every other class that redirects the data
// root: the override is one process-wide static, so running in parallel with the config
// or soundboard tests makes both sides read somebody else's directory.
[Collection("AppPathsSeam")]
public sealed class MonitorPreferencesTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "vonvert-mon-pref-" + Guid.NewGuid().ToString("N"));

    public MonitorPreferencesTests()
    {
        Directory.CreateDirectory(_tempDir);
        AppPaths.RootOverride = _tempDir;
        AppPaths.Invalidate();
    }

    public void Dispose()
    {
        AppPaths.RootOverride = null;
        AppPaths.Invalidate();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact(DisplayName = "PR-001: with no file yet, the shipped defaults are used")]
    public void PR001_DefaultsWithoutFile()
    {
        var prefs = MonitorStore.Load();
        Assert.False(prefs.HasBounds);
        Assert.False(prefs.ShowDry);
        Assert.True(prefs.Topmost);
        Assert.Equal(LufsTargetPreset.Broadcast, prefs.Target);
        Assert.Equal(MonitorLabelMode.Plain, prefs.LabelMode);
    }

    [Fact(DisplayName = "PR-002: what is saved is what comes back")]
    public void PR002_RoundTrip()
    {
        var prefs = MonitorStore.Load();
        prefs.HasBounds = true;
        prefs.Left = 1500; prefs.Top = -820;          // a second monitor above and right
        prefs.Width = 1920; prefs.Height = 1080;
        prefs.ShowDry = true;
        prefs.Topmost = false;
        prefs.Target = LufsTargetPreset.Podcast;
        prefs.LabelMode = MonitorLabelMode.Technical;
        prefs.ReduceMotion = true;         // differs from the default, so the read cannot pass by accident
        prefs.VisualProfile = MonitorVisualProfile.Chroma;
        prefs.ProfileChosen = true;          // the flag that makes an explicit pick outrank the system
        prefs.FontScale = MonitorFontScale.Large;
        prefs.Template = MonitorTemplate.Teaching;
        MonitorStore.Save(prefs);

        var again = MonitorStore.Load();
        Assert.Equal(1500, again.Left);
        Assert.Equal(-820, again.Top);
        Assert.Equal(1920, again.Width);
        Assert.Equal(1080, again.Height);
        Assert.True(again.ShowDry);
        Assert.False(again.Topmost);
        Assert.Equal(LufsTargetPreset.Podcast, again.Target);
        Assert.Equal(MonitorLabelMode.Technical, again.LabelMode);
        Assert.True(again.ReduceMotion);
        Assert.Equal(MonitorVisualProfile.Chroma, again.VisualProfile);
        Assert.True(again.ProfileChosen);
        Assert.Equal(MonitorFontScale.Large, again.FontScale);
        Assert.Equal(MonitorTemplate.Teaching, again.Template);
    }

    [Fact(DisplayName = "PR-003: a half-written file falls back to defaults instead of throwing at startup")]
    public void PR003_CorruptFileIsDefaults()
    {
        File.WriteAllText(MonitorStore.FilePath, "{ this is not json");
        var prefs = MonitorStore.Load();
        Assert.False(prefs.HasBounds);
        Assert.True(prefs.Topmost);
    }

    [Fact(DisplayName = "PR-004: an unknown enum value from an older or newer file is replaced by its default")]
    public void PR004_UnknownEnumValues()
    {
        // Seed with a real save, then corrupt only the enum fields. topmost=false is the
        // sentinel: it differs from the default, so the assertions below cannot pass just
        // because the file was ignored and everything came back at its default.
        var seed = MonitorStore.Load();
        seed.HasBounds = true;
        seed.Left = 10; seed.Top = 12; seed.Width = 800; seed.Height = 600;
        seed.Topmost = false;
        MonitorStore.Save(seed);

        var text = File.ReadAllText(MonitorStore.FilePath)
            .Replace("\"Target\":0", "\"Target\":99")
            .Replace("\"LabelMode\":0", "\"LabelMode\":42")
            .Replace("\"VisualProfile\":0", "\"VisualProfile\":77")
            // Standard is the middle enum value (1), not zero - hence the assert below that
            // the corruption actually applied, which is what caught my wrong guess here.
            .Replace("\"FontScale\":1", "\"FontScale\":77")
            // Diagnose is the first enum value, so zero is the string to corrupt here.
            .Replace("\"Template\":0", "\"Template\":88");
        Assert.Contains("\"Target\":99", text);      // the corruption actually applied
        Assert.Contains("\"LabelMode\":42", text);
        Assert.Contains("\"VisualProfile\":77", text);
        Assert.Contains("\"FontScale\":77", text);
        Assert.Contains("\"Template\":88", text);
        File.WriteAllText(MonitorStore.FilePath, text);

        var prefs = MonitorStore.Load();
        Assert.False(prefs.Topmost);                  // proves the file was read
        Assert.Equal(10, prefs.Left);
        Assert.Equal(800, prefs.Width);
        Assert.Equal(LufsTargetPreset.Broadcast, prefs.Target);
        Assert.Equal(MonitorLabelMode.Plain, prefs.LabelMode);
        Assert.Equal(MonitorVisualProfile.Brand, prefs.VisualProfile);   // unknown number, not a crash
        Assert.Equal(MonitorFontScale.Standard, prefs.FontScale);
        // Safe as a default-vs-default comparison only because topmost=false above proves the
        // file really was read; without that sentinel this assertion would prove nothing.
        Assert.Equal(MonitorTemplate.Diagnose, prefs.Template);
    }

    [Fact(DisplayName = "RB-001: a saved position on a monitor that is gone comes back on the primary screen")]
    public void RB001_OffScreenAfterUnplug()
    {
        var screens = new[] { new ScreenRect(0, 0, 1920, 1080) };
        var (left, top) = MonitorPlacement.Clamp(3000, 40, 1280, 720, screens);
        Assert.InRange(left, 0, 1920 - 1280);
        Assert.InRange(top, 0, 1080 - 720);
        Assert.Equal((1920 - 1280) / 2.0, left);       // centred on the primary
        Assert.Equal((1080 - 720) / 2.0, top);
    }

    [Fact(DisplayName = "RB-002: a position that is already reachable is left exactly where the user put it")]
    public void RB002_InBoundsIsUntouched()
    {
        var screens = new[] { new ScreenRect(0, 0, 1920, 1080), new ScreenRect(1920, 0, 1920, 1080) };
        var (left, top) = MonitorPlacement.Clamp(2100, 300, 1280, 720, screens);
        Assert.Equal(2100, left);
        Assert.Equal(300, top);
    }

    [Fact(DisplayName = "RB-003: a window hanging mostly off the right edge is pulled back to a reachable spot")]
    public void RB003_PartiallyOffScreen()
    {
        var screens = new[] { new ScreenRect(0, 0, 1920, 1080) };
        var (left, top) = MonitorPlacement.Clamp(1700, 1000, 1280, 720, screens);
        Assert.True(left + 1280 <= 1920);
        Assert.True(top + 720 <= 1080);
        Assert.True(left >= 0 && top >= 0);
    }

    [Fact(DisplayName = "RB-004: no screens reported at all still yields a usable position, never a throw")]
    public void RB004_NoScreens()
    {
        var (left, top) = MonitorPlacement.Clamp(500, 500, 1280, 720, Array.Empty<ScreenRect>());
        Assert.Equal(500, left);
        Assert.Equal(500, top);
    }
}
