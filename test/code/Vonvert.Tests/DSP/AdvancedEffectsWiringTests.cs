// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// End-to-end wiring and behaviour guards for the first ported batch
// (RingMod / LoFiReverb / ModulationDelay): present in the default chain and
// off, registered by name, pushed by ApplyPreset, captured back by
// CaptureToProfile, and neutral at zero mix. Mirrors the five-integration-point
// contract pinned by ModulationEffectsWiringTests.

namespace Vonvert.Tests.DSP;

using System.Linq;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Tests.Helpers;
using Xunit;

public class AdvancedEffectsWiringTests
{
    [Fact(DisplayName = "ADV-01: default engine chain instantiates all 3 new effects, all OFF by default")]
    public void NewEffects_PresentInDefaultChain_Disabled()
    {
        var (engine, _) = DspTestRig.New();
        var fx = engine.Effects.Effects;

        Assert.False(fx.OfType<RingModEffect>().Single().IsEnabled);
        Assert.False(fx.OfType<LoFiReverbEffect>().Single().IsEnabled);
        Assert.False(fx.OfType<ModulationDelayEffect>().Single().IsEnabled);
    }

    [Fact(DisplayName = "ADV-02: EffectRegistry exposes each new effect by name and type")]
    public void EffectRegistry_KnowsNewEffects()
    {
        foreach (var (key, type) in new (string, System.Type)[]
        {
            ("RingMod",         typeof(RingModEffect)),
            ("LoFiReverb",      typeof(LoFiReverbEffect)),
            ("ModulationDelay", typeof(ModulationDelayEffect)),
        })
        {
            Assert.Contains(key, EffectRegistry.AllNames);
            Assert.IsType(type, EffectRegistry.Create(key));
        }
    }

    [Fact(DisplayName = "ADV-03: ApplyPreset pushes every new field into the live instances")]
    public void ApplyPreset_NewEffects_ReachEngine()
    {
        var (engine, coord) = DspTestRig.New();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "ADV",
            RingModEnabled = true, RingModCarrierFreq = 250f, RingModMix = 0.7f, RingModHarmonicDepth = 0.4f,
            LoFiReverbEnabled = true, LoFiReverbRoomSize = 0.8f, LoFiReverbDecay = 0.75f,
            LoFiReverbDownsample = 6f, LoFiReverbBitCrush = 4f, LoFiReverbMix = 0.45f,
            ModulationDelayEnabled = true, ModDelayBaseMs = 25f, ModDelayDepth = 0.65f,
            ModDelayFeedback = 0.5f, ModDelayMix = 0.55f,
        });

        var fx = engine.Effects.Effects;

        var rm = fx.OfType<RingModEffect>().Single();
        Assert.True(rm.IsEnabled); Assert.Equal(250f, rm.CarrierFreq, 3);
        Assert.Equal(0.7f, rm.Mix, 3); Assert.Equal(0.4f, rm.HarmonicDepth, 3);

        var lo = fx.OfType<LoFiReverbEffect>().Single();
        Assert.True(lo.IsEnabled); Assert.Equal(0.8f, lo.RoomSize, 3); Assert.Equal(0.75f, lo.Decay, 3);
        Assert.Equal(6, lo.Downsample); Assert.Equal(4f, lo.BitCrush, 3); Assert.Equal(0.45f, lo.Mix, 3);

        var md = fx.OfType<ModulationDelayEffect>().Single();
        Assert.True(md.IsEnabled); Assert.Equal(25f, md.BaseDelayMs, 3); Assert.Equal(0.65f, md.ModDepth, 3);
        Assert.Equal(0.5f, md.Feedback, 3); Assert.Equal(0.55f, md.Mix, 3);
    }

    [Fact(DisplayName = "ADV-04: CaptureToProfile round-trips every new field")]
    public void CaptureToProfile_NewEffects_RoundTrip()
    {
        var (_, coord) = DspTestRig.New();
        var original = new VoiceProfile
        {
            Name = "ADV-RT",
            RingModEnabled = true, RingModCarrierFreq = 320f, RingModMix = 0.65f, RingModHarmonicDepth = 0.25f,
            LoFiReverbEnabled = true, LoFiReverbRoomSize = 0.6f, LoFiReverbDecay = 0.8f,
            LoFiReverbDownsample = 9f, LoFiReverbBitCrush = 5f, LoFiReverbMix = 0.4f,
            ModulationDelayEnabled = true, ModDelayBaseMs = 30f, ModDelayDepth = 0.55f,
            ModDelayFeedback = 0.45f, ModDelayMix = 0.5f,
        };

        coord.ApplyPreset(original);
        var captured = coord.CaptureToProfile();

        Assert.True(captured.RingModEnabled); Assert.Equal(320f, captured.RingModCarrierFreq, 3);
        Assert.Equal(0.65f, captured.RingModMix, 3); Assert.Equal(0.25f, captured.RingModHarmonicDepth, 3);

        Assert.True(captured.LoFiReverbEnabled); Assert.Equal(0.6f, captured.LoFiReverbRoomSize, 3);
        Assert.Equal(0.8f, captured.LoFiReverbDecay, 3); Assert.Equal(9f, captured.LoFiReverbDownsample, 3);
        Assert.Equal(5f, captured.LoFiReverbBitCrush, 3); Assert.Equal(0.4f, captured.LoFiReverbMix, 3);

        Assert.True(captured.ModulationDelayEnabled); Assert.Equal(30f, captured.ModDelayBaseMs, 3);
        Assert.Equal(0.55f, captured.ModDelayDepth, 3); Assert.Equal(0.45f, captured.ModDelayFeedback, 3);
        Assert.Equal(0.5f, captured.ModDelayMix, 3);
    }

    [Fact(DisplayName = "ADV-05: zero mix passes audio through unchanged for all three effects")]
    public void NewEffects_ZeroMix_ArePassthrough()
    {
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);

        IAudioEffect[] fx =
        {
            new RingModEffect         { IsEnabled = true, Mix = 0f },
            new LoFiReverbEffect      { IsEnabled = true, Mix = 0f },
            new ModulationDelayEffect { IsEnabled = true, Mix = 0f },
        };

        foreach (var e in fx)
        {
            var buf = (float[])input.Clone();
            e.Process(buf.AsSpan());
            Assert.True(AudioTestHelpers.SpanEqual(input.AsSpan(), buf.AsSpan()),
                $"{e.Name} at mix 0 must not alter the signal");
        }
    }
}
