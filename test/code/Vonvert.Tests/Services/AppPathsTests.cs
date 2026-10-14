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
using Vonvert.Engine.AudioEngine;
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
        AppPaths.PortableArgsOverride = null;
        AppPaths.PortableExeDirOverride = null;
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

    [Fact(DisplayName = "APX-007: RecordingService derives its folder from AppPaths.Root, so RootOverride keeps it off the real user dir")]
    public void APX007_RecordingService_HonoursRootOverride()
    {
        var root = Path.Combine(_tempDir, "Root");
        Directory.CreateDirectory(root);
        AppPaths.RootOverride = root;
        AppPaths.Invalidate();

        using var svc = new RecordingService();

        Assert.Equal(Path.Combine(root, "Recordings"), svc.RecordingsFolder);
        Assert.StartsWith(root, svc.RecordingsFolder);
    }

    [Theory(DisplayName = "APX-008: IsPortableRequested matches --portable (any case) or the marker file")]
    [InlineData(new[] { "app" }, false, false)]                    // no flag, no marker
    [InlineData(new[] { "app", "--portable" }, false, true)]       // flag
    [InlineData(new[] { "app", "--PORTABLE" }, false, true)]       // case-insensitive
    [InlineData(new[] { "app" }, true, true)]                      // marker file present
    public void APX008_IsPortableRequested(string[] args, bool markerExists, bool expect)
    {
        var exeDir = Path.Combine(_tempDir, "exe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exeDir);
        if (markerExists) File.WriteAllText(Path.Combine(exeDir, "vonvert.portable"), "");
        Assert.Equal(expect, AppPaths.IsPortableRequested(args, exeDir));
    }

    [Fact(DisplayName = "APX-009: portable wins over the pointer and points Root at exe\\data")]
    public void APX009_Portable_IgnoresPointer_AndUsesExeData()
    {
        var exeDir = Path.Combine(_tempDir, "portable_exe");
        Directory.CreateDirectory(exeDir);
        // A pointer that would otherwise redirect elsewhere must be ignored.
        AppPaths.PointerDirOverride = _tempDir;
        File.WriteAllText(AppPaths.PointerFilePath, Path.Combine(_tempDir, "SomewhereElse"));
        AppPaths.RootOverride = null;
        AppPaths.PortableArgsOverride = new[] { "app", "--portable" };
        AppPaths.PortableExeDirOverride = exeDir;
        AppPaths.Invalidate();

        Assert.Equal(Path.Combine(exeDir, "data"), AppPaths.Root);
        Assert.True(AppPaths.IsPortable);
    }

    [Fact(DisplayName = "APX-010: an exe dir that cannot host a writable data folder falls back, not portable")]
    public void APX010_Portable_NotWritable_FallsBack()
    {
        // Point the "exe dir" at an existing FILE: creating <file>\data throws, so the
        // portable branch must give up and the normal pointer/default logic applies.
        var fakeExeFile = Path.Combine(_tempDir, "not_a_dir.txt");
        File.WriteAllText(fakeExeFile, "x");
        AppPaths.RootOverride = null;
        AppPaths.PointerDirOverride = _tempDir;
        File.Delete(AppPaths.PointerFilePath); // no pointer -> default root expected
        AppPaths.PortableArgsOverride = new[] { "app", "--portable" };
        AppPaths.PortableExeDirOverride = fakeExeFile;
        AppPaths.Invalidate();

        Assert.Equal(AppPaths.DefaultRoot, AppPaths.Root);
        Assert.False(AppPaths.IsPortable);
    }

    [Theory(DisplayName = "APX-011: mutex name is global when installed, per-dir when portable")]
    [InlineData(true)]
    [InlineData(false)]
    public void APX011_MutexNameFor(bool portable)
    {
        var installed = AppPaths.MutexNameFor(false, @"C:\Any");
        Assert.Equal("Vonvert_SingleInstance_Mutex", installed);

        string a = AppPaths.MutexNameFor(true, @"C:\Apps\VonvertA");
        string a2 = AppPaths.MutexNameFor(true, @"C:\Apps\VonvertA\\"); // trailing sep = same dir
        string b = AppPaths.MutexNameFor(true, @"D:\VonvertB");
        Assert.StartsWith("Vonvert_Portable_", a);
        Assert.Equal(a, a2);          // deterministic
        Assert.NotEqual(a, b);        // different dir -> different lock
    }
}
