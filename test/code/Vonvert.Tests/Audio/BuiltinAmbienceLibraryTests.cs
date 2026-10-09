// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using System.Linq;
using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  BuiltinAmbienceLibrary — catalog of the original, procedurally
//  synthesized ambience clips embedded in Vonvert.Engine. The catalog
//  must stay in lock-step with the embedded WAV resources (a renamed or
//  dropped resource would silently break the BGM picker), so each entry
//  is checked for its resource bytes and lazy extraction to cache.
// ═══════════════════════════════════════════════════════════════════

public sealed class BuiltinAmbienceLibraryTests : IDisposable
{
    private readonly string _tempDir;

    public BuiltinAmbienceLibraryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_Ambience_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    [Fact(DisplayName = "AMBIENCE-001: catalog holds the ten built-in clips with unique ids")]
    public void AMBIENCE001_TenClips()
    {
        var ids = BuiltinAmbienceLibrary.All.Select(s => s.Id).ToList();
        Assert.Equal(10, BuiltinAmbienceLibrary.All.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("rain", ids);
        Assert.Contains("white-noise", ids);
        Assert.Contains("campfire", ids);
    }

    [Fact(DisplayName = "AMBIENCE-002: every catalog clip's embedded WAV resource is present and non-trivial")]
    public void AMBIENCE002_EmbeddedBytesResolve()
    {
        Assert.Equal("Vonvert.Engine.AudioEngine.BuiltinAmbience.", BuiltinAmbienceLibrary.EmbeddedPrefix);
        foreach (var s in BuiltinAmbienceLibrary.All)
        {
            var bytes = BuiltinAmbienceLibrary.GetBuiltinBytes(s.FileName);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 100_000, $"{s.Id} resource too small ({bytes.Length})");
            // RIFF/WAVE magic proves it is a real WAV, not a placeholder.
            Assert.Equal((byte)'R', bytes[0]);
            Assert.Equal((byte)'I', bytes[1]);
            Assert.Equal((byte)'F', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
    }

    [Fact(DisplayName = "AMBIENCE-003: ResolveToCache writes the clip to disk with the embedded byte length")]
    public void AMBIENCE003_ResolveToCache()
    {
        var sound = BuiltinAmbienceLibrary.All.First(s => s.Id == "rain");
        var path = BuiltinAmbienceLibrary.ResolveToCache(sound, _tempDir);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        var expected = BuiltinAmbienceLibrary.GetBuiltinBytes(sound.FileName)!.Length;
        Assert.Equal(expected, new FileInfo(path!).Length);
    }

    [Fact(DisplayName = "AMBIENCE-004: ResolveToCache is idempotent (second call keeps the same file)")]
    public void AMBIENCE004_ResolveIdempotent()
    {
        var sound = BuiltinAmbienceLibrary.All.First(s => s.Id == "ocean");
        var p1 = BuiltinAmbienceLibrary.ResolveToCache(sound, _tempDir);
        var p2 = BuiltinAmbienceLibrary.ResolveToCache(sound, _tempDir);
        Assert.Equal(p1, p2);
        Assert.True(File.Exists(p2));
    }

    [Fact(DisplayName = "AMBIENCE-005: unknown/missing resource resolves to null rather than throwing")]
    public void AMBIENCE005_MissingResource()
    {
        Assert.Null(BuiltinAmbienceLibrary.GetBuiltinBytes("does-not-exist.wav"));
        var ghost = new BuiltinAmbienceSound("ghost", "ghost.wav", "Ghost", "test");
        Assert.Null(BuiltinAmbienceLibrary.ResolveToCache(ghost, _tempDir));
    }
}
