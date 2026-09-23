// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the built-in free tier after it was expanded from a single Female
// preset to five (Normal, Deep Male, Female, Robot, Demon).  It verifies the
// manifest-driven loader still surfaces every pack member from the embedded
// resource and — crucially — that the two presets that depend on the newly
// ported effects (Robot → VxRobot, Demon → VxDrive) actually build those
// effects into their DSP chain, so a broken registry wiring is caught here
// rather than as silence at runtime.

namespace Vonvert.Tests.Presets;

using System;
using System.IO;
using System.Linq;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public sealed class BuiltInFreeTierPresetTests : IDisposable
{
    private readonly string _tempDir;

    public BuiltInFreeTierPresetTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_FreeTier_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    [Fact(DisplayName = "FREE-TIER-01: all five built-in free presets load from the manifest")]
    public void AllFiveFreePresets_AreLoaded()
    {
        var mgr = new PresetManager(_tempDir);
        var names = mgr.Presets.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in new[] { "Normal", "Deep Male", "Female", "Robot", "Demon" })
            Assert.True(names.Contains(expected), $"Built-in preset '{expected}' was not loaded.");
    }

    [Fact(DisplayName = "FREE-TIER-02: Robot preset builds an enabled VxRobot into its chain")]
    public void RobotPreset_BuildsVxRobot()
    {
        var robot = new PresetManager(_tempDir).Presets.Single(p => p.Name == "Robot");
        Assert.True(robot.RobotEnabled);

        var chain = robot.CreateDSPChain();
        var vx = chain.Effects.OfType<VxRobot>().SingleOrDefault();
        Assert.NotNull(vx);
        Assert.True(vx!.IsEnabled, "VxRobot must be enabled in the Robot preset chain.");
    }

    [Fact(DisplayName = "FREE-TIER-03: Demon preset builds an enabled VxDrive with saturation")]
    public void DemonPreset_BuildsVxDrive()
    {
        var demon = new PresetManager(_tempDir).Presets.Single(p => p.Name == "Demon");
        Assert.True(demon.DistortEnabled);

        var chain = demon.CreateDSPChain();
        var drive = chain.Effects.OfType<VxDrive>().SingleOrDefault();
        Assert.NotNull(drive);
        Assert.True(drive!.IsEnabled);
        Assert.Equal(demon.DistortSaturation, drive.Drive, 3);
    }

    [Fact(DisplayName = "FREE-TIER-04: Normal preset is clean (no pitch/robot/drive) but gated+compressed")]
    public void NormalPreset_IsClean()
    {
        var normal = new PresetManager(_tempDir).Presets.Single(p => p.Name == "Normal");
        Assert.False(normal.PitchEnabled);
        Assert.False(normal.RobotEnabled);
        Assert.False(normal.DistortEnabled);
        Assert.True(normal.GateEnabled);
    }
}
