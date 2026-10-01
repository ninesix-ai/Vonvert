// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// End-to-end wiring guard for the second batch of ported effects (TempoDelay,
// Expander, DuckingCompressor). The runtime applies presets through
// DspParameterCoordinator.ApplyPreset against the fixed effect instances that
// MainWindow.BindDSPEffects extracts from the engine chain, so a missing link in
// any of the five integration points (effect class / EffectRegistry / default
// chain / coordinator / BindDSPEffects) silently drops the preset. These tests
// pin the whole loop: presence in the default chain, registry lookup, apply, and
// capture round-trip.

namespace Vonvert.Tests.DSP;

using System.Linq;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class SecondBatchEffectsWiringTests
{
    [Fact(DisplayName = "SB-01: default engine chain instantiates the second batch, all OFF by default")]
    public void SecondBatch_PresentInDefaultChain_Disabled()
    {
        var (engine, _) = DspTestRig.New();
        var fx = engine.Effects.Effects;

        var tempo = fx.OfType<TempoDelayEffect>().Single();
        var exp   = fx.OfType<ExpanderEffect>().Single();
        var duck  = fx.OfType<VxDuckingCompressor>().Single();

        Assert.False(tempo.IsEnabled);
        Assert.False(exp.IsEnabled);
        Assert.False(duck.IsEnabled);
    }

    [Fact(DisplayName = "SB-02: EffectRegistry exposes the second batch by name (key == effect Name)")]
    public void EffectRegistry_KnowsSecondBatch()
    {
        Assert.Contains("TempoDelay", EffectRegistry.AllNames);
        Assert.Contains("Expander", EffectRegistry.AllNames);
        Assert.Contains("DuckingCompressor", EffectRegistry.AllNames);

        Assert.IsType<TempoDelayEffect>(EffectRegistry.Create("TempoDelay"));
        Assert.IsType<ExpanderEffect>(EffectRegistry.Create("Expander"));
        Assert.IsType<VxDuckingCompressor>(EffectRegistry.Create("DuckingCompressor"));
    }

    [Fact(DisplayName = "SB-03: ApplyPreset pushes TempoDelay into the live effect (division included)")]
    public void ApplyPreset_TempoDelay_ReachesEngine()
    {
        var (engine, coord) = DspTestRig.New();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "TD", TempoDelayEnabled = true, TempoBpm = 90f,
            TempoDivision = (int)TempoNoteDivision.Quarter, TempoFeedback = 0.6f,
            TempoDamping = 0.4f, TempoMix = 0.5f,
        });

        var tempo = engine.Effects.Effects.OfType<TempoDelayEffect>().Single();
        Assert.True(tempo.IsEnabled);
        Assert.Equal(90f, tempo.Bpm, 5);
        Assert.Equal(TempoNoteDivision.Quarter, tempo.Division);
        Assert.Equal(0.6f, tempo.Feedback, 5);
        Assert.Equal(0.4f, tempo.Damping, 5);
        Assert.Equal(0.5f, tempo.Mix, 5);
    }

    [Fact(DisplayName = "SB-04: ApplyPreset pushes Expander thresholds and timing into the live effect")]
    public void ApplyPreset_Expander_ReachesEngine()
    {
        var (engine, coord) = DspTestRig.New();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "EX", ExpanderEnabled = true, ExpanderThresholdDb = -25f,
            ExpanderRatio = 3f, ExpanderAttackMs = 2f, ExpanderReleaseMs = 120f, ExpanderRangeDb = -50f,
        });

        var exp = engine.Effects.Effects.OfType<ExpanderEffect>().Single();
        Assert.True(exp.IsEnabled);
        Assert.Equal(-25f, exp.ThresholdDb, 5);
        Assert.Equal(3f, exp.Ratio, 5);
        Assert.Equal(2f, exp.AttackMs, 5);
        Assert.Equal(120f, exp.ReleaseMs, 5);
        Assert.Equal(-50f, exp.RangeDb, 5);
    }

    [Fact(DisplayName = "SB-05: ApplyPreset pushes DuckingCompressor into the live effect")]
    public void ApplyPreset_Ducking_ReachesEngine()
    {
        var (engine, coord) = DspTestRig.New();
        coord.ApplyPreset(new VoiceProfile
        {
            Name = "DK", DuckingEnabled = true, DuckThresholdDb = -15f,
            DuckRatio = 6f, DuckAttackMs = 4f, DuckReleaseMs = 300f, DuckRangeDb = -30f,
        });

        var duck = engine.Effects.Effects.OfType<VxDuckingCompressor>().Single();
        Assert.True(duck.IsEnabled);
        Assert.Equal(-15f, duck.ThresholdDb, 5);
        Assert.Equal(6f, duck.Ratio, 5);
        Assert.Equal(4f, duck.AttackMs, 5);
        Assert.Equal(300f, duck.ReleaseMs, 5);
        Assert.Equal(-30f, duck.RangeDb, 5);
    }

    [Fact(DisplayName = "SB-06: CaptureToProfile round-trips every second-batch field")]
    public void CaptureToProfile_SecondBatch_RoundTrip()
    {
        var (_, coord) = DspTestRig.New();
        var original = new VoiceProfile
        {
            Name = "RT",
            TempoDelayEnabled = true, TempoBpm = 150f, TempoDivision = (int)TempoNoteDivision.Eighth,
            TempoFeedback = 0.5f, TempoDamping = 0.2f, TempoMix = 0.45f,
            ExpanderEnabled = true, ExpanderThresholdDb = -35f, ExpanderRatio = 2.5f,
            ExpanderAttackMs = 1.5f, ExpanderReleaseMs = 80f, ExpanderRangeDb = -45f,
            DuckingEnabled = true, DuckThresholdDb = -18f, DuckRatio = 5f,
            DuckAttackMs = 3f, DuckReleaseMs = 250f, DuckRangeDb = -25f,
        };

        coord.ApplyPreset(original);
        var captured = coord.CaptureToProfile();

        Assert.True(captured.TempoDelayEnabled);
        Assert.Equal(150f, captured.TempoBpm, 5);
        Assert.Equal(7f, captured.TempoDivision, 5);
        Assert.Equal(0.5f, captured.TempoFeedback, 5);
        Assert.Equal(0.2f, captured.TempoDamping, 5);
        Assert.Equal(0.45f, captured.TempoMix, 5);

        Assert.True(captured.ExpanderEnabled);
        Assert.Equal(-35f, captured.ExpanderThresholdDb, 5);
        Assert.Equal(2.5f, captured.ExpanderRatio, 5);
        Assert.Equal(1.5f, captured.ExpanderAttackMs, 5);
        Assert.Equal(80f, captured.ExpanderReleaseMs, 5);
        Assert.Equal(-45f, captured.ExpanderRangeDb, 5);

        Assert.True(captured.DuckingEnabled);
        Assert.Equal(-18f, captured.DuckThresholdDb, 5);
        Assert.Equal(5f, captured.DuckRatio, 5);
        Assert.Equal(3f, captured.DuckAttackMs, 5);
        Assert.Equal(250f, captured.DuckReleaseMs, 5);
        Assert.Equal(-25f, captured.DuckRangeDb, 5);
    }
}
