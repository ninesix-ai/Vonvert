// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.Services;

using System;
using System.IO;
using Vonvert.Engine;
using Vonvert.Engine.Services;
using Xunit;

// Verifies the soundboard live-mode flag round-trips through app-config.json.
// Serialized into the AppPaths seam collection because it redirects the data root.
[Collection("AppPathsSeam")]
public sealed class AppConfigSoundboardTests : IDisposable
{
    private readonly string _tempDir;
    public AppConfigSoundboardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_CfgSb_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        AppPaths.RootOverride = _tempDir;
        AppPaths.Invalidate();
    }
    public void Dispose()
    {
        AppConfig.Instance.Audio.SoundboardLiveMode = false; // avoid cross-test bleed
        AppConfig.Instance.Audio.FloatLeft = null;
        AppConfig.Instance.Audio.FloatTop = null;
        AppConfig.Instance.Audio.FloatAutoOpen = false;
        AppConfig.Instance.Audio.FloatTopmost = true;
        AppPaths.RootOverride = null;
        AppPaths.PointerDirOverride = null;
        AppPaths.Invalidate();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact(DisplayName = "CFG-SB-001: SoundboardLiveMode persists across Save/Load")]
    public void SoundboardLiveMode_RoundTrips()
    {
        var cfg = AppConfig.Instance;
        cfg.Audio.SoundboardLiveMode = true;
        cfg.Save();

        cfg.Audio.SoundboardLiveMode = false;   // mutate in-memory
        cfg.Load();                              // reload from disk
        Assert.True(cfg.Audio.SoundboardLiveMode);
    }

    [Fact(DisplayName = "CFG-SB-002: floating-window state fields round-trip through Save/Load")]
    public void FloatingState_RoundTrips()
    {
        // Only the round-trip is asserted on the shared instance: AppConfig.Instance is a
        // process-wide singleton and other parallel collections may already have touched it,
        // so "what the defaults are" is answered on a fresh AudioSection below.
        var cfg = AppConfig.Instance;

        cfg.Audio.FloatLeft = 240;
        cfg.Audio.FloatTop = 180;
        cfg.Audio.FloatAutoOpen = true;
        cfg.Audio.FloatTopmost = false;
        cfg.Save();

        cfg.Audio.FloatLeft = null; cfg.Audio.FloatTop = null;
        cfg.Audio.FloatAutoOpen = false; cfg.Audio.FloatTopmost = true;
        cfg.Load();

        Assert.Equal(240, cfg.Audio.FloatLeft!.Value);
        Assert.Equal(180, cfg.Audio.FloatTop!.Value);
        Assert.True(cfg.Audio.FloatAutoOpen);
        Assert.False(cfg.Audio.FloatTopmost);
    }

    [Fact(DisplayName = "CFG-SB-003: floating-window fields start at sensible defaults")]
    public void FloatingState_Defaults()
    {
        // A detached section object: proves the shipped defaults (window starts unpositioned,
        // topmost, no auto-open) without depending on any global/singleton state.
        var fresh = new AppConfig.AudioSection();
        Assert.True(fresh.FloatTopmost);
        Assert.False(fresh.FloatAutoOpen);
        Assert.Null(fresh.FloatLeft);
        Assert.Null(fresh.FloatTop);
    }
}
