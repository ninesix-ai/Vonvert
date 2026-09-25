// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the built-in free tier: the manifest-driven loader must surface every
// pack member from the embedded resource, and the presets that depend on
// specific effects must actually build those effects into their DSP chain — so
// a registry or profile-wiring regression is caught here rather than as
// silence at runtime.

namespace Vonvert.Tests.Presets;

using System;
using System.IO;
using System.Linq;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
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

    [Fact(DisplayName = "FREE-TIER-01: all eight built-in free presets load from the manifest")]
    public void AllEightFreePresets_AreLoaded()
    {
        var mgr = new PresetManager(_tempDir);
        var names = mgr.Presets.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in new[] { "Normal", "Deep Male", "Female", "Robot", "Demon", "Android", "Radio Ghost", "Tape Wobble" })
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

    [Fact(DisplayName = "FREE-TIER-05: Android preset builds an enabled RingMod into its chain")]
    public void AndroidPreset_BuildsRingMod()
    {
        var p = new PresetManager(_tempDir).Presets.Single(x => x.Name == "Android");
        Assert.True(p.RingModEnabled);

        var fx = p.CreateDSPChain().Effects.OfType<RingModEffect>().SingleOrDefault();
        Assert.NotNull(fx);
        Assert.True(fx!.IsEnabled);
        // Assert the literal from free.json, not p.RingModCarrierFreq — comparing
        // profile against effect would also pass if the JSON key were misspelled
        // and both sides silently held the type default.
        Assert.Equal(120f, p.RingModCarrierFreq, 3);
        Assert.Equal(120f, fx.CarrierFreq, 3);
    }

    [Fact(DisplayName = "FREE-TIER-06: Radio Ghost preset builds an enabled LoFiReverb into its chain")]
    public void RadioGhostPreset_BuildsLoFiReverb()
    {
        var p = new PresetManager(_tempDir).Presets.Single(x => x.Name == "Radio Ghost");
        Assert.True(p.LoFiReverbEnabled);

        var fx = p.CreateDSPChain().Effects.OfType<LoFiReverbEffect>().SingleOrDefault();
        Assert.NotNull(fx);
        Assert.True(fx!.IsEnabled);
        Assert.Equal(8f, p.LoFiReverbDownsample, 3);
        Assert.Equal(8, fx.Downsample);
        Assert.Equal(5f, p.LoFiReverbBitCrush, 3);
    }

    [Fact(DisplayName = "FREE-TIER-07: Tape Wobble preset builds an enabled ModulationDelay into its chain")]
    public void TapeWobblePreset_BuildsModulationDelay()
    {
        var p = new PresetManager(_tempDir).Presets.Single(x => x.Name == "Tape Wobble");
        Assert.True(p.ModulationDelayEnabled);

        var fx = p.CreateDSPChain().Effects.OfType<ModulationDelayEffect>().SingleOrDefault();
        Assert.NotNull(fx);
        Assert.True(fx!.IsEnabled);
        Assert.Equal(18f, p.ModDelayBaseMs, 3);
        Assert.Equal(18f, fx.BaseDelayMs, 3);
    }
}
