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
}
