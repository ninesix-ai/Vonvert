// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// End-to-end wiring + behaviour guards for the modulation effect family
// (Flanger / Phaser / Tremolo / Vibrato) ported into the GitHub engine.
// Mirrors the five-integration-point contract from NewEffectsWiringTests:
// a modulation effect only takes effect if it is present in the default
// chain, registered by name, pushed by ApplyPreset, and captured back by
// CaptureToProfile. The behavioural cases pin the deterministic edges
// (zero-mix passthrough and setter clamping) of each DSP algorithm.

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

public class ModulationEffectsWiringTests
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
            fx.OfType<FlangerEffect>().Single(), fx.OfType<PhaserEffect>().Single(), fx.OfType<TremoloEffect>().Single(), fx.OfType<VibratoEffect>().Single(),
            fx.OfType<RingModEffect>().Single(), fx.OfType<LoFiReverbEffect>().Single(), fx.OfType<ModulationDelayEffect>().Single());
        return (engine, coord);
    }

    [Fact(DisplayName = "MOD-01: default engine chain instantiates all 4 modulation effects, all OFF by default")]
    public void ModulationEffects_PresentInDefaultChain_Disabled()
    {
        var (engine, _) = NewRig();
        var fx = engine.Effects.Effects;

        Assert.False(fx.OfType<FlangerEffect>().Single().IsEnabled);
        Assert.False(fx.OfType<PhaserEffect>().Single().IsEnabled);
        Assert.False(fx.OfType<TremoloEffect>().Single().IsEnabled);
        Assert.False(fx.OfType<VibratoEffect>().Single().IsEnabled);
    }

    [Fact(DisplayName = "MOD-02: EffectRegistry exposes each modulation effect by name and type")]
    public void EffectRegistry_KnowsModulationEffects()
    {
        foreach (var (key, type) in new (string, System.Type)[]
        {
            ("VoiceFlanger", typeof(FlangerEffect)),
            ("VoicePhaser",  typeof(PhaserEffect)),
            ("VoiceTremolo", typeof(TremoloEffect)),
            ("VoiceVibrato", typeof(VibratoEffect)),
        })
        {
            Assert.Contains(key, EffectRegistry.AllNames);
            Assert.IsType(type, EffectRegistry.Create(key));
        }
    }

    [Fact(DisplayName = "MOD-03: ApplyPreset pushes every modulation field into the live instances")]
    public void ApplyPreset_Modulation_ReachesEngine()
    {
        var (engine, coord) = NewRig();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "M",
            FlangerEnabled = true, FlangerRate = 2f, FlangerDepth = 0.6f, FlangerFeedback = 0.4f, FlangerMix = 0.7f,
            PhaserEnabled = true, PhaserRate = 3f, PhaserDepth = 0.8f, PhaserFeedback = -0.3f, PhaserMix = 0.5f,
            TremoloEnabled = true, TremoloRate = 8f, TremoloDepth = 0.9f,
            VibratoEnabled = true, VibratoRate = 6f, VibratoDepth = 0.35f, VibratoMix = 0.8f,
        });

        var fx = engine.Effects.Effects;
        var fl = fx.OfType<FlangerEffect>().Single();
        Assert.True(fl.IsEnabled); Assert.Equal(2f, fl.Rate, 5); Assert.Equal(0.6f, fl.Depth, 5);
        Assert.Equal(0.4f, fl.Feedback, 5); Assert.Equal(0.7f, fl.Mix, 5);

        var ph = fx.OfType<PhaserEffect>().Single();
        Assert.True(ph.IsEnabled); Assert.Equal(3f, ph.Rate, 5); Assert.Equal(-0.3f, ph.Feedback, 5);

        var tr = fx.OfType<TremoloEffect>().Single();
        Assert.True(tr.IsEnabled); Assert.Equal(8f, tr.Rate, 5); Assert.Equal(0.9f, tr.Depth, 5);

        var vb = fx.OfType<VibratoEffect>().Single();
        Assert.True(vb.IsEnabled); Assert.Equal(6f, vb.Rate, 5); Assert.Equal(0.35f, vb.Depth, 5); Assert.Equal(0.8f, vb.Mix, 5);
    }

    [Fact(DisplayName = "MOD-04: CaptureToProfile round-trips every modulation field")]
    public void CaptureToProfile_Modulation_RoundTrip()
    {
        var (_, coord) = NewRig();
        var original = new VoiceProfile
        {
            Name = "RT",
            FlangerEnabled = true, FlangerRate = 1.5f, FlangerDepth = 0.2f, FlangerFeedback = 0.8f, FlangerMix = 0.6f,
            PhaserEnabled = true, PhaserRate = 2.5f, PhaserDepth = 0.4f, PhaserFeedback = -0.5f, PhaserMix = 0.3f,
            TremoloEnabled = true, TremoloRate = 6f, TremoloDepth = 0.7f,
            VibratoEnabled = true, VibratoRate = 4f, VibratoDepth = 0.5f, VibratoMix = 0.9f,
        };

        coord.ApplyPreset(original);
        var captured = coord.CaptureToProfile();

        Assert.True(captured.FlangerEnabled); Assert.Equal(1.5f, captured.FlangerRate, 5); Assert.Equal(0.8f, captured.FlangerFeedback, 5);
        Assert.True(captured.PhaserEnabled);  Assert.Equal(2.5f, captured.PhaserRate, 5);  Assert.Equal(-0.5f, captured.PhaserFeedback, 5);
        Assert.True(captured.TremoloEnabled); Assert.Equal(6f, captured.TremoloRate, 5);   Assert.Equal(0.7f, captured.TremoloDepth, 5);
        Assert.True(captured.VibratoEnabled); Assert.Equal(4f, captured.VibratoRate, 5);   Assert.Equal(0.9f, captured.VibratoMix, 5);
    }

    [Fact(DisplayName = "MOD-05: zero-mix Flanger/Phaser/Vibrato and zero-depth Tremolo pass audio through unchanged")]
    public void ModulationEffects_NeutralSettings_ArePassthrough()
    {
        var input = Enumerable.Range(0, 512).Select(i => 0.5f).ToArray();

        var flanger = new FlangerEffect { IsEnabled = true, Mix = 0f };
        var phaser  = new PhaserEffect  { IsEnabled = true, Mix = 0f };
        var vibrato = new VibratoEffect { IsEnabled = true, Mix = 0f };
        var tremolo = new TremoloEffect { IsEnabled = true, Depth = 0f };

        foreach (IAudioEffect fx in new IAudioEffect[] { flanger, phaser, vibrato, tremolo })
        {
            var buf = (float[])input.Clone();
            fx.Process(buf);
            Assert.Equal(input, buf);   // fully dry / zero depth → no change
        }
    }

    [Fact(DisplayName = "MOD-06: full-depth Tremolo amplitude-modulates a constant signal")]
    public void Tremolo_FullDepth_ModulatesAmplitude()
    {
        var tremolo = new TremoloEffect { IsEnabled = true, Rate = 10f, Depth = 1f };
        var buf = Enumerable.Repeat(1f, 4800).ToArray();
        tremolo.Process(buf);

        Assert.Contains(buf, v => v < 0.9f);   // some samples are turned down
        Assert.Contains(buf, v => v > 0.9f);   // and some remain near full
        Assert.DoesNotContain(buf, v => float.IsNaN(v) || float.IsInfinity(v));
    }

    [Fact(DisplayName = "MOD-07: parameter setters clamp out-of-range values to their documented bounds")]
    public void ModulationEffects_Setters_Clamp()
    {
        var fl = new FlangerEffect { Rate = 1000f, Depth = 5f, Feedback = 9f, Mix = -2f };
        Assert.Equal(10f, fl.Rate, 5);
        Assert.Equal(1f, fl.Depth, 5);
        Assert.Equal(0.95f, fl.Feedback, 5);
        Assert.Equal(0f, fl.Mix, 5);

        var ph = new PhaserEffect { Rate = -5f, Stages = 100 };
        Assert.Equal(0.1f, ph.Rate, 5);
        Assert.Equal(8, ph.Stages);

        var tr = new TremoloEffect { Rate = 0f, Depth = 3f };
        Assert.Equal(0.5f, tr.Rate, 5);
        Assert.Equal(1f, tr.Depth, 5);

        var vb = new VibratoEffect { Rate = 999f, Mix = 4f };
        Assert.Equal(12f, vb.Rate, 5);
        Assert.Equal(1f, vb.Mix, 5);
    }
}
