// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the data-location resolution that lets users relocate Vonvert's user data.
// Focus on the pure decision (ResolveFromPointer / IsUsablePath) and a pointer
// round-trip exercised through the internal test seams — never touching the real
// %APPDATA% default root.

namespace Vonvert.Tests.Services;

using System;
using System.IO;
using Vonvert.Engine;
using Xunit;

[Collection("AppPathsSeam")]
public sealed class AppPathsTests : IDisposable
{
    private const string FakeDefault = @"C:\Users\test\AppData\Roaming\ninesix-ai\Vonvert";
    private readonly string _tempDir;

    public AppPathsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_AppPaths_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Always release the static seams so no other test observes them.
        AppPaths.RootOverride = null;
        AppPaths.PointerDirOverride = null;
        AppPaths.Invalidate();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Theory(DisplayName = "APX-001: empty / whitespace pointer falls back to the default root")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void APX001_EmptyPointer_UsesDefault(string? content)
        => Assert.Equal(FakeDefault, AppPaths.ResolveFromPointer(content, FakeDefault));

    [Theory(DisplayName = "APX-002: a relative or character-invalid pointer is rejected")]
    [InlineData("relative/path")]
    [InlineData("C:\\bad|pipe")]
    public void APX002_UnusablePointer_UsesDefault(string content)
        => Assert.Equal(FakeDefault, AppPaths.ResolveFromPointer(content, FakeDefault));

    [Fact(DisplayName = "APX-003: a rooted, valid pointer wins (surrounding whitespace trimmed)")]
    public void APX003_ValidPointer_Used()
    {
        var target = @"D:\VonvertData\me";
        Assert.Equal(target, AppPaths.ResolveFromPointer("  " + target + "\n", FakeDefault));
    }

    [Theory(DisplayName = "APX-004: IsUsablePath accepts only rooted, character-valid paths")]
    [InlineData(@"C:\a\b", true)]
    [InlineData(@"\\server\share", true)]
    [InlineData("not-rooted", false)]
    [InlineData("", false)]
    [InlineData("C:\\has?invalid", false)]
    public void APX004_IsUsablePath(string path, bool expected)
        => Assert.Equal(expected, AppPaths.IsUsablePath(path));

    [Fact(DisplayName = "APX-005: TrySetRoot + Root round-trips through the pointer file (no real AppData touched)")]
    public void APX005_SetRoot_RoundTrips()
    {
        AppPaths.PointerDirOverride = _tempDir;
        AppPaths.RootOverride = null;
        AppPaths.Invalidate();

        // No pointer yet → default.
        Assert.Equal(AppPaths.DefaultRoot, AppPaths.Root);

        var target = Path.Combine(_tempDir, "RelocatedData");
        Assert.True(AppPaths.TrySetRoot(target));

        AppPaths.Invalidate();
        Assert.Equal(target, AppPaths.Root);
    }

    [Fact(DisplayName = "APX-006: TrySetRoot rejects an unusable path and writes nothing")]
    public void APX006_TrySetRoot_RejectsInvalid()
    {
        AppPaths.PointerDirOverride = _tempDir;
        Assert.False(AppPaths.TrySetRoot("relative-nope"));
        Assert.False(File.Exists(AppPaths.PointerFilePath));
    }
}
