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
        public long LastToken = 7;          // canned non-zero token
        public string? LastSoundId;
        public long Play(byte[] pcm, float volume, string? soundId = null)
        { Calls++; LastSoundId = soundId; return LastToken; }
        public bool TryGetProgress(long token, out int position, out int length)
        { position = 0; length = 0; return false; }
    }

    [Fact(DisplayName = "SBPlay-001: preview mode plays local-only, no engine broadcast")]
    public void Preview_LocalOnly()
    {
        var aud = new CountingAudition();
        var sb = new SoundboardManager { Audition = aud, LiveMode = false };
        using var engine = new VoiceEngine();
        Assert.Equal(7, sb.Play("kick", engine));         // audition token is surfaced
        Assert.Equal(1, aud.Calls);
        Assert.Equal("kick", aud.LastSoundId);            // pad id forwarded for replacement
        Assert.Equal(0, engine.SoundboardVoices);         // not broadcast
    }

    [Fact(DisplayName = "SBPlay-002: live mode plays local AND broadcasts once")]
    public void Live_DualOutput()
    {
        var aud = new CountingAudition();
        var sb = new SoundboardManager { Audition = aud, LiveMode = true };
        using var engine = new VoiceEngine();
        Assert.Equal(7, sb.Play("kick", engine));
        Assert.Equal(1, aud.Calls);
        Assert.Equal(1, engine.SoundboardVoices);          // broadcast
    }

    [Fact(DisplayName = "SBPlay-003: null Audition degrades to token 0 without throwing")]
    public void NullAudition_NoThrow()
    {
        var sb = new SoundboardManager { Audition = null, LiveMode = false };
        using var engine = new VoiceEngine();
        var ex = Record.Exception(() => sb.Play("kick", engine));
        Assert.Null(ex);
        Assert.Equal(0, sb.Play("kick", engine));          // no local voice → no token
    }
}
