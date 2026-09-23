// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Focused unit tests for the parts of HotkeyService that are pure or file-based
// and therefore safe to exercise in CI: the default binding set, human-readable
// key formatting, conflict detection, binding equality, and JSON persistence
// hardening (valid round-trip + rejection of out-of-range virtual keys).
//
// RegisterHotKey / the low-level PTT keyboard hook are intentionally NOT touched
// here — they require a live window handle and an interactive desktop, so a
// headless unit test would either no-op or install a real system hook. Those
// paths are guarded by the compile-time wiring and by manual verification.

namespace Vonvert.Tests.UIServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vonvert.App.UIServices;
using Xunit;

public sealed class HotkeyServiceTests : IDisposable
{
    private readonly string _tempDir;

    public HotkeyServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_Hotkey_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    private string ConfigPathFor(string name) => Path.Combine(_tempDir, name + ".json");

    [Fact(DisplayName = "HK-DEFAULT-01: four default actions bound to F9–F12 with no modifiers")]
    public void DefaultBindings_AreF9ToF12()
    {
        var b = HotkeyService.DefaultBindings;
        Assert.Equal(4, b.Count);
        Assert.Equal(0x78, b[HotkeyService.HotkeyAction.TogglePower].VirtualKey);   // F9
        Assert.Equal(0x79, b[HotkeyService.HotkeyAction.ToggleMute].VirtualKey);    // F10
        Assert.Equal(0x7A, b[HotkeyService.HotkeyAction.CyclePreset].VirtualKey);   // F11
        Assert.Equal(0x7B, b[HotkeyService.HotkeyAction.PushToTalk].VirtualKey);    // F12
        Assert.All(b.Values, v => Assert.False(v.Ctrl || v.Alt || v.Shift));
    }

    [Theory(DisplayName = "HK-FMT: key formatting shortens modifiers, letters, digits and OEM keys")]
    [InlineData(0x78, false, false, false, "F9")]                    // plain function key
    [InlineData(0x78, true,  false, false, "Ctrl+F9")]              // one modifier
    [InlineData(0x41, true,  true, true, "Ctrl+Alt+Shift+A")]       // single letter upper-cased
    [InlineData(0x31, true,  false, false, "Ctrl+1")]               // D1 → 1
    public void FormatKey_ShapesCommonCases(int vk, bool ctrl, bool alt, bool shift, string expected)
    {
        var binding = new HotkeyService.HotkeyBinding { VirtualKey = vk, Ctrl = ctrl, Alt = alt, Shift = shift };
        Assert.Equal(expected, HotkeyService.FormatKey(binding));
    }

    [Fact(DisplayName = "HK-CONF-01: FindConflict detects a clash on another action and excludes self")]
    public void FindConflict_DetectsCrossActionClash()
    {
        var hk = new HotkeyService();   // defaults; no registration performed
        var f9 = new HotkeyService.HotkeyBinding { VirtualKey = 0x78 };   // == TogglePower

        // Re-using F9 while editing ToggleMute conflicts with TogglePower.
        Assert.Equal(HotkeyService.HotkeyAction.TogglePower,
            hk.FindConflict(HotkeyService.HotkeyAction.ToggleMute, f9));

        // The same key is NOT a conflict when it's the action already owning it.
        Assert.Null(hk.FindConflict(HotkeyService.HotkeyAction.TogglePower, f9));

        // A free key yields no conflict.
        Assert.Null(hk.FindConflict(HotkeyService.HotkeyAction.ToggleMute,
            new HotkeyService.HotkeyBinding { VirtualKey = 0x70 }));   // F1, unbound
    }

    [Fact(DisplayName = "HK-EQ-01: Matches compares VK + all modifiers; Clone deep-copies")]
    public void MatchesAndClone()
    {
        var a = new HotkeyService.HotkeyBinding { VirtualKey = 0x41, Ctrl = true };
        var same = new HotkeyService.HotkeyBinding { VirtualKey = 0x41, Ctrl = true };
        var diffMod = new HotkeyService.HotkeyBinding { VirtualKey = 0x41, Ctrl = false, Alt = true };

        Assert.True(a.Matches(same));
        Assert.False(a.Matches(diffMod));

        var clone = a.Clone();
        clone.Ctrl = false;
        Assert.True(a.Ctrl);            // original untouched → deep copy
    }

    [Fact(DisplayName = "HK-CFG-01: bindings persist to and reload from the JSON config")]
    public void Config_RoundTripsCustomBinding()
    {
        var path = ConfigPathFor("roundtrip");

        var save = new HotkeyService { ConfigPathOverride = path };
        save.Bindings[HotkeyService.HotkeyAction.CyclePreset] =
            new HotkeyService.HotkeyBinding { VirtualKey = 0x41, Ctrl = true };   // Ctrl+A
        save.SaveConfig();
        Assert.True(File.Exists(path));

        var load = new HotkeyService { ConfigPathOverride = path };
        load.LoadConfig();
        var loaded = load.Bindings[HotkeyService.HotkeyAction.CyclePreset];
        Assert.Equal(0x41, loaded.VirtualKey);
        Assert.True(loaded.Ctrl);
    }

    [Fact(DisplayName = "HK-CFG-02: an out-of-range virtual key in the config is rejected, default kept")]
    public void Config_RejectsOutOfRangeVirtualKey()
    {
        var path = ConfigPathFor("badvk");
        // VirtualKey 0 is below the usable Win32 range (0x01–0xFE).
        File.WriteAllText(path,
            """{"ToggleMute":{"VirtualKey":0,"Ctrl":false,"Alt":false,"Shift":false}}""");

        var load = new HotkeyService { ConfigPathOverride = path };
        load.LoadConfig();
        Assert.Equal(0x79, load.Bindings[HotkeyService.HotkeyAction.ToggleMute].VirtualKey);   // F10 default survives
    }

    [Fact(DisplayName = "HK-CFG-03: the Push-to-Talk hold mode persists and reloads")]
    public void Config_RoundTripsPttHoldMode()
    {
        var path = ConfigPathFor("pttmode");

        var save = new HotkeyService { ConfigPathOverride = path, PttHoldToMute = true };
        save.SaveConfig();
        Assert.True(File.Exists(path));

        var load = new HotkeyService { ConfigPathOverride = path };
        Assert.False(load.PttHoldToMute);        // default before load
        load.LoadConfig();
        Assert.True(load.PttHoldToMute);         // Hold-to-Mute survives restart
    }

    [Fact(DisplayName = "HK-CFG-04: a legacy config without the hold-mode key defaults to Hold-to-Talk")]
    public void Config_LegacyFlatFormat_DefaultsHoldToTalk()
    {
        var path = ConfigPathFor("legacy");
        // Older builds wrote only the flat binding map (no "_pttHoldToMute").
        File.WriteAllText(path,
            """{"PushToTalk":{"VirtualKey":123,"Ctrl":false,"Alt":false,"Shift":false}}""");

        var load = new HotkeyService { ConfigPathOverride = path };
        load.LoadConfig();
        Assert.False(load.PttHoldToMute);
        Assert.Equal(123, load.Bindings[HotkeyService.HotkeyAction.PushToTalk].VirtualKey);   // F12
    }
}
