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
}
