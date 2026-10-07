// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the command-line purge-mode detection used by App.OnStartup.

namespace Vonvert.Tests.Services;

using Vonvert.App;
using Xunit;

public sealed class StartupArgsTests
{
    // A1: presence of the purge flag (alone or among other args) -> purge mode.
    [Fact]
    public void DetectsPurgeFlag()
    {
        Assert.True(StartupArgs.IsPurgeMode(new[] { "--purge-user-data" }));
        Assert.True(StartupArgs.IsPurgeMode(new[] { "/other", StartupArgs.PurgeFlag }));
    }

    // A2: null / empty / unrelated args -> not purge mode.
    [Fact]
    public void IgnoresOtherArgs()
    {
        Assert.False(StartupArgs.IsPurgeMode(null));
        Assert.False(StartupArgs.IsPurgeMode(System.Array.Empty<string>()));
        Assert.False(StartupArgs.IsPurgeMode(new[] { "--minimized" }));
    }

    // A3: --purge-root <dir> surfaces the explicit sandbox root.
    [Fact]
    public void DetectsPurgeRoot()
    {
        Assert.Equal(@"D:\vpurge-sandbox",
            StartupArgs.GetPurgeRoot(new[] { "--purge-user-data", "--purge-root", @"D:\vpurge-sandbox" }));
    }

    // A4: absent / missing-value / next-token-is-a-flag / blank -> null (normal purge).
    [Fact]
    public void PurgeRootAbsentOrMalformed_ReturnsNull()
    {
        Assert.Null(StartupArgs.GetPurgeRoot(null));
        Assert.Null(StartupArgs.GetPurgeRoot(new[] { "--purge-user-data" }));
        Assert.Null(StartupArgs.GetPurgeRoot(new[] { "--purge-root" }));
        Assert.Null(StartupArgs.GetPurgeRoot(new[] { "--purge-root", "--other" }));
        Assert.Null(StartupArgs.GetPurgeRoot(new[] { "--purge-root", "  " }));
    }
}
