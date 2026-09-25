// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Covers the pure purge-target resolution: default/redirected/legacy roots, pointer
// fallback on empty/relative/wildcard content, de-duplication, and the guarantee that
// a user's recordings directory never leaks into the delete set.

namespace Vonvert.Tests.Services;

using System.Linq;
using Vonvert.Engine;
using Xunit;

public sealed class PurgeTargetResolverTests
{
    private const string Default = @"C:\u\AppData\Roaming\ninesix-ai\Vonvert";
    private const string Legacy  = @"C:\u\AppData\Roaming\Vonvert";
    private const string Crash   = @"C:\u\AppData\Local\Temp\Vonvert_crash.txt";

    // P1: no pointer -> only the default root (plus optional legacy), never a custom root.
    [Fact]
    public void NoPointer_ReturnsOnlyDefaultRoot()
    {
        var t = PurgeTargetResolver.Resolve(null, Default, null, null);
        Assert.Equal(new[] { Default }, t.Directories);
    }

    // P2: valid pointer -> custom root AND default root are both targeted.
    [Fact]
    public void ValidPointer_IncludesCustomAndDefaultRoot()
    {
        const string custom = @"D:\VonvertData";
        var t = PurgeTargetResolver.Resolve(custom, Default, null, null);
        Assert.Contains(custom, t.Directories);
        Assert.Contains(Default, t.Directories);
        Assert.Equal(2, t.Directories.Count);
    }

    // P3: whitespace-only pointer -> falls back to default only.
    [Fact]
    public void EmptyPointer_FallsBackToDefault()
    {
        var t = PurgeTargetResolver.Resolve("   ", Default, null, null);
        Assert.Equal(new[] { Default }, t.Directories);
    }

    // P4: relative-path pointer -> rejected, default only.
    [Fact]
    public void RelativePointer_FallsBackToDefault()
    {
        var t = PurgeTargetResolver.Resolve("VonvertData", Default, null, null);
        Assert.Equal(new[] { Default }, t.Directories);
    }

    // P5: wildcard chars -> rejected, default only.
    [Fact]
    public void WildcardPointer_FallsBackToDefault()
    {
        var t = PurgeTargetResolver.Resolve(@"D:\a?b", Default, null, null);
        Assert.Equal(new[] { Default }, t.Directories);
    }

    // P6: legacy bare root provided -> included alongside default.
    [Fact]
    public void LegacyProvided_Included()
    {
        var t = PurgeTargetResolver.Resolve(null, Default, Legacy, null);
        Assert.Contains(Legacy, t.Directories);
        Assert.Contains(Default, t.Directories);
    }

    // P7: pointer that resolves to the default root -> de-duplicated, single entry.
    [Fact]
    public void CustomEqualsDefault_Deduped()
    {
        var t = PurgeTargetResolver.Resolve(Default, Default, null, null);
        Assert.Single(t.Directories);
        Assert.Equal(Default, t.Directories[0]);
    }

    // P8: crash dump lands in Files, registry key is set, and a recordings-like path
    // is never present in Directories (nothing but the known roots may appear there).
    [Fact]
    public void CarriesFilesAndRegistry_NeverIncludesRecordings()
    {
        var t = PurgeTargetResolver.Resolve(null, Default, Legacy, Crash);
        Assert.Contains(Crash, t.Files);
        Assert.Equal(@"Software\Vonvert", t.RegistrySubKey);
        Assert.DoesNotContain(Crash, t.Directories);
        // Only the two known roots are ever in Directories.
        Assert.All(t.Directories, d => Assert.True(d == Default || d == Legacy));
    }
}
