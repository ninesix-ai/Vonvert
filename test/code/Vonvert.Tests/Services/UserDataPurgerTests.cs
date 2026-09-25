// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Integration tests for the purge executor against temp directories and an injected
// fake registry hive. Verifies recursive deletion, pointer-redirect handling, legacy
// root presence/absence, registry + crash-dump cleanup, idempotency, and fault
// tolerance. The real HKCU and real %APPDATA% are never touched.

namespace Vonvert.Tests.Services;

using System;
using System.Collections.Generic;
using System.IO;
using Vonvert.Engine;
using Xunit;

public sealed class UserDataPurgerTests : IDisposable
{
    private readonly string _base;

    public UserDataPurgerTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "vpurge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_base);
    }

    public void Dispose()
    {
        try { Directory.Delete(_base, true); } catch { /* best effort */ }
    }

    private string Dir(string name)
    {
        var p = Path.Combine(_base, name);
        Directory.CreateDirectory(p);
        return p;
    }

    private static string WriteFile(string dir, string rel, string content = "x")
    {
        var full = Path.Combine(dir, rel);
        var parent = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(parent);
        File.WriteAllText(full, content);
        return full;
    }

    private sealed class FakeRegistry : IRegistryCleaner
    {
        public bool KeyExists { get; set; }
        public bool ThrowOn { get; set; }
        public List<string> Deleted { get; } = new();
        public bool DeleteSubKeyTree(string key)
        {
            if (ThrowOn) throw new UnauthorizedAccessException("denied");
            if (!KeyExists) return false;
            Deleted.Add(key);
            return true;
        }
    }

    // E1: only the default root exists -> deleted recursively, exit code 0.
    [Fact]
    public void DeletesDefaultRootRecursively()
    {
        var def = Dir("def");
        WriteFile(def, "config.json");
        WriteFile(def, "sub/preset.json");

        var r = new UserDataPurger(new FakeRegistry()).Purge(new PurgeOptions { DefaultRoot = def });

        Assert.False(Directory.Exists(def));
        Assert.Contains(def, r.DeletedPaths);
        Assert.Equal(0, r.ExitCode);
    }

    // E2: pointer -> custom root; both custom and default roots are removed.
    [Fact]
    public void DeletesCustomAndDefaultRoot()
    {
        var custom = Dir("custom"); WriteFile(custom, "a.txt");
        var def = Dir("def"); WriteFile(def, "b.txt");
        WriteFile(def, "data_location.txt", custom);

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, PointerFilePath = Path.Combine(def, "data_location.txt") });

        Assert.False(Directory.Exists(custom));
        Assert.False(Directory.Exists(def));
    }

    // E3: pointer holds an invalid (relative) path -> only default root removed, no throw.
    [Fact]
    public void InvalidPointer_OnlyDefault()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        WriteFile(def, "data_location.txt", "relative/x");

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, PointerFilePath = Path.Combine(def, "data_location.txt") });

        Assert.False(Directory.Exists(def));
        Assert.Equal(0, r.ExitCode);
    }

    // E4: pointer file absent -> only default root removed.
    [Fact]
    public void NoPointerFile_OnlyDefault()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, PointerFilePath = Path.Combine(def, "data_location.txt") });

        Assert.False(Directory.Exists(def));
    }

    // E5: legacy bare root present -> removed.
    [Fact]
    public void LegacyRootPresent_Deleted()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        var legacy = Dir("legacy"); WriteFile(legacy, "c.txt");

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, LegacyBareRoot = legacy });

        Assert.False(Directory.Exists(legacy));
        Assert.Contains(legacy, r.DeletedPaths);
    }

    // E6: legacy bare root absent -> skipped, no error.
    [Fact]
    public void LegacyRootMissing_Skipped()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        var missing = Path.Combine(_base, "legacy-missing");

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, LegacyBareRoot = missing });

        Assert.Contains(missing, r.SkippedPaths);
        Assert.Equal(0, r.ExitCode);
    }

    // E7: registry key present -> removed via the injected cleaner.
    [Fact]
    public void DeletesRegistryKey()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        var reg = new FakeRegistry { KeyExists = true };

        var r = new UserDataPurger(reg).Purge(new PurgeOptions { DefaultRoot = def });

        Assert.Contains(@"Software\Vonvert", reg.Deleted);
        Assert.Contains(@"HKCU\Software\Vonvert", r.DeletedPaths);
    }

    // E8: crash dump present -> removed.
    [Fact]
    public void DeletesCrashDump()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        var crash = WriteFile(Dir("tmp"), "Vonvert_crash.txt");

        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = def, CrashDumpPath = crash });

        Assert.False(File.Exists(crash));
        Assert.Contains(crash, r.DeletedPaths);
    }

    // E9: nothing exists -> idempotent, exit code 0, nothing deleted, some skipped.
    [Fact]
    public void MissingEverything_IsIdempotent()
    {
        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = Path.Combine(_base, "nope"), LegacyBareRoot = Path.Combine(_base, "nope2") });

        Assert.Equal(0, r.ExitCode);
        Assert.Empty(r.DeletedPaths);
        Assert.NotEmpty(r.SkippedPaths);
    }

    // E10: registry throws -> directories still deleted, failure recorded, exit code 1.
    [Fact]
    public void RegistryFailure_DoesNotAbortAndSetsExitCode()
    {
        var def = Dir("def"); WriteFile(def, "b.txt");
        var reg = new FakeRegistry { ThrowOn = true };

        var r = new UserDataPurger(reg).Purge(new PurgeOptions { DefaultRoot = def });

        Assert.False(Directory.Exists(def));
        Assert.Equal(1, r.ExitCode);
        Assert.NotEmpty(r.FailedPaths);
    }

    // E11: minimal options (default root absent) -> no throw.
    [Fact]
    public void EmptyOptions_NoThrow()
    {
        var r = new UserDataPurger(new FakeRegistry())
            .Purge(new PurgeOptions { DefaultRoot = Path.Combine(_base, "empty") });

        Assert.NotNull(r);
    }
}
