// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// End-to-end wiring guard for the effects ported into the minimal GitHub
// engine. The runtime applies presets through DspParameterCoordinator.ApplyPreset
// (NOT VoiceProfile.CreateDSPChain), against the fixed set of effect instances
// MainWindow.BindDSPEffects extracts from the engine chain. A missing link in
// any of the five integration points (effect class / EffectRegistry / default
// chain / coordinator / BindDSPEffects) silently drops the preset, so this
// file pins the whole loop for TiltEQ, GraphicEQ and Bitcrusher.

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

public class NewEffectsWiringTests
{
    private static (VoiceEngine engine, DspParameterCoordinator coord) NewRig()
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
        return (engine, coord);
    }

    [Fact(DisplayName = "WIRE-01: default engine chain instantiates the 3 new effects, all OFF by default")]
    public void NewEffects_PresentInDefaultChain_Disabled()
    {
        var (engine, _) = NewRig();
        var fx = engine.Effects.Effects;

        var tilt = fx.OfType<TiltEQEffect>().Single();
        var geq  = fx.OfType<GraphicEQEffect>().Single();
        var bit  = fx.OfType<BitcrusherEffect>().Single();

        Assert.False(tilt.IsEnabled);
        Assert.False(geq.IsEnabled);
        Assert.False(bit.IsEnabled);
    }

    [Fact(DisplayName = "WIRE-02: EffectRegistry exposes the new effects by name")]
    public void EffectRegistry_KnowsNewEffects()
    {
        Assert.Contains("TiltEQ", EffectRegistry.AllNames);
        Assert.Contains("GraphicEQ", EffectRegistry.AllNames);
        Assert.Contains("Bitcrusher", EffectRegistry.AllNames);

        Assert.IsType<TiltEQEffect>(EffectRegistry.Create("TiltEQ"));
        Assert.IsType<GraphicEQEffect>(EffectRegistry.Create("GraphicEQ"));
        Assert.IsType<BitcrusherEffect>(EffectRegistry.Create("Bitcrusher"));
        // StereoWidth is deliberately NOT wired into the minimal chain.
        Assert.DoesNotContain("VoiceStereoWidth", EffectRegistry.AllNames);
    }

    [Fact(DisplayName = "WIRE-03: ApplyPreset pushes Tilt EQ into the live effect instance")]
    public void ApplyPreset_TiltEq_ReachesEngine()
    {
        var (engine, coord) = NewRig();
        coord.ApplyPreset(new VoiceProfile { Name = "T", TiltEqEnabled = true, Tilt = 0.7f });

        var tilt = engine.Effects.Effects.OfType<TiltEQEffect>().Single();
        Assert.True(tilt.IsEnabled);
        Assert.Equal(0.7f, tilt.Tilt, 5);
    }

    [Fact(DisplayName = "WIRE-04: ApplyPreset pushes all 10 Graphic EQ bands into the live effect")]
    public void ApplyPreset_GraphicEq_ReachesEngine()
    {
        var (engine, coord) = NewRig();
        var gains = Enumerable.Range(0, 10).Select(i => (float)(i - 5)).ToArray(); // -5..4 dB
        coord.ApplyPreset(new VoiceProfile { Name = "G", GraphicEqEnabled = true, GraphicEqGains = gains });

        var geq = engine.Effects.Effects.OfType<GraphicEQEffect>().Single();
        Assert.True(geq.IsEnabled);
        for (int b = 0; b < 10; b++)
            Assert.Equal(gains[b], geq.GainsDb[b], 5);
    }

    [Fact(DisplayName = "WIRE-05: ApplyPreset pushes Bitcrusher depth and sample reduction")]
    public void ApplyPreset_Bitcrusher_ReachesEngine()
    {
        var (engine, coord) = NewRig();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "B", BitcrusherEnabled = true, BitcrusherBitDepth = 4f, BitcrusherSampleReduction = 6f
        });

        var bit = engine.Effects.Effects.OfType<BitcrusherEffect>().Single();
        Assert.True(bit.IsEnabled);
        Assert.Equal(4f, bit.BitDepth, 5);
        Assert.Equal(6, bit.SampleRateReduction);
    }

    [Fact(DisplayName = "WIRE-06: CaptureToProfile round-trips every new-effect field")]
    public void CaptureToProfile_NewEffects_RoundTrip()
    {
        var (_, coord) = NewRig();
        var gains = new float[] { 1f, 2f, 3f, 4f, 5f, -1f, -2f, -3f, -4f, -5f };
        var original = new VoiceProfile
        {
            Name = "RT",
            TiltEqEnabled = true, Tilt = -0.5f,
            GraphicEqEnabled = true, GraphicEqGains = (float[])gains.Clone(),
            BitcrusherEnabled = true, BitcrusherBitDepth = 6f, BitcrusherSampleReduction = 3f,
        };

        coord.ApplyPreset(original);
        var captured = coord.CaptureToProfile();

        Assert.True(captured.TiltEqEnabled);
        Assert.Equal(-0.5f, captured.Tilt, 5);
        Assert.True(captured.GraphicEqEnabled);
        Assert.Equal(gains, captured.GraphicEqGains);
        Assert.True(captured.BitcrusherEnabled);
        Assert.Equal(6f, captured.BitcrusherBitDepth, 5);
        Assert.Equal(3f, captured.BitcrusherSampleReduction, 5);
    }

    [Fact(DisplayName = "WIRE-07: captured profile is decoupled from the live effect's gain array")]
    public void CaptureToProfile_DoesNotAliasGainArray()
    {
        var (engine, coord) = NewRig();
        coord.ApplyPreset(new VoiceProfile { Name = "A", GraphicEqEnabled = true, GraphicEqGains = new float[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0 } });
        var captured = coord.CaptureToProfile();

        // Mutating the live effect afterwards must not retro-edit the snapshot.
        var geq = engine.Effects.Effects.OfType<GraphicEQEffect>().Single();
        geq.SetBandGain(0, -9f);

        Assert.Equal(3f, captured.GraphicEqGains[0]);
    }
}
