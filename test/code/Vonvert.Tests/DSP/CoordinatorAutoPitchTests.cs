// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Tests.DSP;

using System.Linq;
using Vonvert.App;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Engine.PresetLibrary;
using Xunit;

// Verifies ApplyPreset wires the auto-pitch closed loop: profile.AutoPitchTarget
// must reach both VxPitch.AutoTargetEnabled and the pipeline's normalizer, and
// stay off by default (no behavior change for the existing Female preset).
public class CoordinatorAutoPitchTests
{
    private static (VoiceEngine engine, DspParameterCoordinator coord, VxPitch pitch, NullAudioProcessor proc) NewRig()
    {
        var engine = new VoiceEngine();
        var fx = engine.Effects.Effects;
        var coord = new DspParameterCoordinator(
            engine,
            fx.OfType<VxPitch>().Single(), fx.OfType<VxReverb>().Single(), fx.OfType<VxChorus>().Single(),
            fx.OfType<VxCompressor>().Single(), fx.OfType<VxGate>().Single(), fx.OfType<VxEq>().Single(),
            fx.OfType<NoiseReductionEffect>().Single(), fx.OfType<DelayEffect>().Single(), fx.OfType<DeesserEffect>().Single(),
            fx.OfType<VxRobot>().Single(), fx.OfType<VxDrive>().Single(),
            fx.OfType<TiltEQEffect>().Single(), fx.OfType<GraphicEQEffect>().Single(), fx.OfType<BitcrusherEffect>().Single(),
            fx.OfType<FlangerEffect>().Single(), fx.OfType<PhaserEffect>().Single(), fx.OfType<TremoloEffect>().Single(), fx.OfType<VibratoEffect>().Single());
        return (engine, coord, fx.OfType<VxPitch>().Single(), (NullAudioProcessor)engine.Pipeline);
    }

    [Fact]
    public void DefaultProfile_AutoStaysOff()
    {
        var (_, coord, pitch, proc) = NewRig();
        coord.ApplyPreset(new VoiceProfile { Name = "F", PitchEnabled = true, PitchOffset = 4, AutoPitchTarget = false });
        Assert.False(pitch.AutoTargetEnabled);
        Assert.False(proc.PitchNormalizer.Enabled);
    }

    [Fact]
    public void AutoProfile_TurnsLoopOn()
    {
        var (_, coord, pitch, proc) = NewRig();
        coord.ApplyPreset(new VoiceProfile { Name = "F", PitchEnabled = true, PitchOffset = 4, AutoPitchTarget = true, TargetF0Hz = 220f });
        Assert.True(pitch.AutoTargetEnabled);
        Assert.NotNull(pitch.AutoSource);
        Assert.True(proc.PitchNormalizer.Enabled);
        Assert.Equal(220f, proc.PitchNormalizer.TargetF0Hz);
        Assert.Equal(4f, proc.PitchNormalizer.FallbackSemitones);
    }
}
