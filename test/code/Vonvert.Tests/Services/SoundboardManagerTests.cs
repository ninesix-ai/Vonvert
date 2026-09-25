// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the de-licensed soundboard manager: the full built-in set is accessible with
// no licensing surface, hotkey assignment + lookup round-trips, bindings persist to the
// data folder, and generated sound data is cached verbatim. Shares the "AppPathsSeam"
// collection so it never runs in parallel with AppPathsTests (both mutate the static root).

namespace Vonvert.Tests.Services;

using System;
using System.IO;
using System.Linq;
using Xunit;
using Vonvert.Engine;
using Vonvert.Engine.ProceduralAudio;
using Vonvert.Engine.Soundboard;

[Collection("AppPathsSeam")]
public sealed class SoundboardManagerTests : IDisposable
{
    private readonly string _tempDir;

    public SoundboardManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_Soundboard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        AppPaths.RootOverride = _tempDir;
        AppPaths.Invalidate();
    }

    public void Dispose()
    {
        AppPaths.RootOverride = null;
        AppPaths.PointerDirOverride = null;
        AppPaths.Invalidate();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact(DisplayName = "SBMgr-001: all built-ins are accessible and the licensing surface is gone")]
    public void SBMgr001_AllAccessible_NoLicensing()
    {
        var sb = new SoundboardManager();
        Assert.Equal(50, sb.Sounds.Count);
        Assert.Equal(7,  sb.Categories.Count);

        var t = typeof(SoundboardManager);
        Assert.Null(t.GetMethod("IsSoundAccessible"));
        Assert.Null(t.GetMethod("IsSlotAccessible"));
        Assert.Null(t.GetProperty("IsExpandedLibraryAvailable"));
    }

    [Fact(DisplayName = "SBMgr-002: assigning a hotkey is findable and replaces same-key conflicts")]
    public void SBMgr002_AssignAndFindHotkey()
    {
        var sb = new SoundboardManager();
        sb.AssignHotkey("kick",  new SoundHotkeyBinding(0x41, Ctrl: true));
        sb.AssignHotkey("snare", new SoundHotkeyBinding(0x41, Ctrl: true)); // same key → replaces kick

        Assert.Equal("snare", sb.FindSoundByHotkey(0x41, true, false, false));
        Assert.Null(sb.GetHotkey("kick"));
        Assert.Null(sb.FindSoundByHotkey(0x41, false, false, false)); // modifiers must match
    }

    [Fact(DisplayName = "SBMgr-003: hotkey bindings persist and reload from the data folder")]
    public void SBMgr003_HotkeyPersistenceRoundTrip()
    {
        var a = new SoundboardManager();
        a.AssignHotkey("airhorn", new SoundHotkeyBinding(0x75, Alt: true));

        var b = new SoundboardManager(); // fresh instance, same redirected root
        var reloaded = b.GetHotkey("airhorn");
        Assert.NotNull(reloaded);
        Assert.Equal(0x75, reloaded!.VirtualKey);
        Assert.True(reloaded.Alt);
        Assert.True(File.Exists(Path.Combine(_tempDir, "soundboard-hotkeys.json")));
    }

    [Fact(DisplayName = "SBMgr-004: GetSoundData caches generated PCM identical to SoundGenerator bytes")]
    public void SBMgr004_GeneratedSoundDataMatchesGenerator()
    {
        var sb = new SoundboardManager();
        var data = sb.GetSoundData("kick");
        Assert.NotEmpty(data);
        Assert.Equal(SoundGenerator.GenerateBytes("kick"), data);
    }

    [Fact(DisplayName = "SBMgr-005: import then remove round-trips a user sound")]
    public void SBMgr005_ImportRemoveRoundTrip()
    {
        var sb = new SoundboardManager();
        int before = sb.UserSlotCount;

        // ImportFile copies + registers metadata only (it does not parse audio), so any
        // bytes with a supported extension are enough to exercise the add/remove path.
        var src = Path.Combine(_tempDir, "sample.wav");
        File.WriteAllBytes(src, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        var def = sb.ImportFile(src);

        Assert.Equal(SoundSourceType.File, def.SourceType);
        Assert.Equal(before + 1, sb.UserSlotCount);
        Assert.Contains(sb.Sounds, s => s.Id == def.Id);

        Assert.True(sb.RemoveUserSound(def.Id));
        Assert.Equal(before, sb.UserSlotCount);
        Assert.DoesNotContain(sb.Sounds, s => s.Id == def.Id);
    }
}
