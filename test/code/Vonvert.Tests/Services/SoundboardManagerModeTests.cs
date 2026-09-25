// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.Services;

using System;
using System.IO;
using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.Soundboard;
using Xunit;

// Guards the mode-based playback routing: preview mode is local-only, live mode also
// broadcasts through the engine, and a null audition channel never throws. No licensing
// or upgrade gating is reintroduced (see SoundboardManagerTests.SBMgr-001).
[Collection("AppPathsSeam")]
public sealed class SoundboardManagerModeTests : IDisposable
{
    private readonly string _tempDir;
    public SoundboardManagerModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_SbMode_" + Guid.NewGuid().ToString("N"));
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

    private sealed class CountingAudition : IAuditionPlayer
    {
        public int Calls;
        public void Play(byte[] pcm, float volume) => Calls++;
    }

    [Fact(DisplayName = "SBPlay-001: preview mode plays local-only, no engine broadcast")]
    public void Preview_LocalOnly()
    {
        var aud = new CountingAudition();
        var sb = new SoundboardManager { Audition = aud, LiveMode = false };
        using var engine = new VoiceEngine();
        sb.Play("kick", engine);
        Assert.Equal(1, aud.Calls);
        Assert.Equal(0, engine.SoundboardVoices);   // not broadcast
    }

    [Fact(DisplayName = "SBPlay-002: live mode plays local AND broadcasts once")]
    public void Live_DualOutput()
    {
        var aud = new CountingAudition();
        var sb = new SoundboardManager { Audition = aud, LiveMode = true };
        using var engine = new VoiceEngine();
        sb.Play("kick", engine);
        Assert.Equal(1, aud.Calls);
        Assert.Equal(1, engine.SoundboardVoices);    // broadcast
    }

    [Fact(DisplayName = "SBPlay-003: null Audition degrades without throwing")]
    public void NullAudition_NoThrow()
    {
        var sb = new SoundboardManager { Audition = null, LiveMode = false };
        using var engine = new VoiceEngine();
        var ex = Record.Exception(() => sb.Play("kick", engine));
        Assert.Null(ex);
    }
}
