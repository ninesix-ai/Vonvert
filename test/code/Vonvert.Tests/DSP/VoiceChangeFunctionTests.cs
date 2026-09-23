// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// End-to-end "voice change" functional test — walks the SAME path the UI uses:
// VoiceEngine → default DSP chain → DspParameterCoordinator.ApplyPreset(Female)
// → Normal-compare processing. Verifies, without any audio hardware, that a
// synthetic voice signal: (1) flows through the chain (non-silent output),
// (2) is actually processed (differs from the dry input), and (3) is pitched
// upward (+4 semitones ≈ ×1.26) by the Female preset.

namespace Vonvert.Tests.DSP;

using Vonvert.App;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Tests.Helpers;
using Xunit;

public class VoiceChangeFunctionTests
{
    /// <summary>The single built-in female voice (mirror of free.json).</summary>
    private static VoiceProfile FemaleVoice() => new()
    {
        Name           = "Female",
        PitchEnabled   = true,
        PitchOffset    = 4,
        EqEnabled      = true,
        EqLowDb        = -4,
        EqLowFreq      = 160f,
        EqMidDb        = 3,
        EqMidFreq      = 2600f,
        EqMidQ         = 1.1f,
        EqHighDb       = 3,
        EqHighFreq     = 9000f,
        CompressEnabled = true,
        CompStrength   = 3,
        CompCeilingDb  = -15f,
        ChorusEnabled  = true,
        ChorusSpeed    = 1.2f,
        ChorusDepth    = 0.12f,
        ChorusMix      = 0.22f,
        DeesserEnabled = true,
        DeesserFreq    = 6500f,
        DeesserThreshDb = -20f,
        DeesserRatio   = 3f,
        DeesserQ       = 1.5f,
    };

    private static DspParameterCoordinator CreateCoordinator(VoiceEngine engine)
    {
        var effects = engine.Effects.Effects;
        return new DspParameterCoordinator(
            engine,
            effects.OfType<VxPitch>().Single(),
            effects.OfType<VxReverb>().Single(),
            effects.OfType<VxChorus>().Single(),
            effects.OfType<VxCompressor>().Single(),
            effects.OfType<VxGate>().Single(),
            effects.OfType<VxEq>().Single(),
            effects.OfType<NoiseReductionEffect>().Single(),
            effects.OfType<DelayEffect>().Single(),
            effects.OfType<DeesserEffect>().Single(),
            effects.OfType<VxRobot>().Single(),
            effects.OfType<VxDrive>().Single(),
            effects.OfType<TiltEQEffect>().Single(),
            effects.OfType<GraphicEQEffect>().Single(),
            effects.OfType<BitcrusherEffect>().Single(),
            effects.OfType<FlangerEffect>().Single(),
            effects.OfType<PhaserEffect>().Single(),
            effects.OfType<TremoloEffect>().Single(),
            effects.OfType<VibratoEffect>().Single());
    }

    /// <summary>Process a signal exactly like the engine worker loop (CompareMode.Normal).</summary>
    private static void RunThroughEngine(IAudioProcessor pipeline, float[] signal)
    {
        pipeline.PreMix(signal, signal.Length);
        pipeline.Process(signal);
        pipeline.PostAnalyze(signal, signal.Length);
    }

    [Fact]
    public void FemalePreset_SignalFlows_OutputNotSilent()
    {
        var engine = new VoiceEngine();
        CreateCoordinator(engine).ApplyPreset(FemaleVoice());

        var signal = AudioTestHelpers.GenerateVoiceLike(180f, 32768);
        RunThroughEngine(engine.Pipeline, signal);

        double rms = AudioTestHelpers.ComputeRms(signal.AsSpan(16384));
        Assert.True(rms > 1e-3, $"Output RMS = {rms:G4} — expected non-silent (signal must flow through the chain)");
    }

    [Fact]
    public void FemalePreset_OutputDiffersFromDry()
    {
        var engine = new VoiceEngine();
        CreateCoordinator(engine).ApplyPreset(FemaleVoice());

        var signal = AudioTestHelpers.GenerateVoiceLike(180f, 32768);
        var dry = (float[])signal.Clone();

        RunThroughEngine(engine.Pipeline, signal);

        double diff = AudioTestHelpers.ComputeRmsDifference(dry.AsSpan(), signal.AsSpan());
        Assert.True(diff > 0.05, $"Dry vs Female-output RMS difference = {diff:P2} — expected the preset to alter the signal");
    }

    [Fact]
    public void FemalePreset_PitchShiftedUp_RoughlyPlus4Semitones()
    {
        // +4 semitones ≈ ×2^(4/12) ≈ ×1.26 → 180 Hz ≈ 227 Hz. A clean single
        // tone is used so the zero-crossing estimator stays unambiguous even
        // after the full chain (EQ phase response + chorus blend the spectrum).
        const float inputFreq = 180f;
        var engine = new VoiceEngine();
        CreateCoordinator(engine).ApplyPreset(FemaleVoice());

        var signal = AudioTestHelpers.GenerateSine(inputFreq, 32768);
        RunThroughEngine(engine.Pipeline, signal);

        double outputFreq = AudioTestHelpers.EstimateDominantFrequency(signal.AsSpan(16384));
        if (outputFreq < 200 || outputFreq > 260)
        {
            // Diagnostic: measure the dominant frequency after EACH stage so a
            // regression shows exactly which effect killed the pitch shift.
            var buf = AudioTestHelpers.GenerateSine(inputFreq, 32768);
            var stages = new List<string>();
            foreach (var fx in engine.Effects.Effects)
            {
                fx.Process(buf);
                stages.Add($"{fx.Name}={(fx.IsEnabled ? "on" : "off")}->{AudioTestHelpers.EstimateDominantFrequency(buf.AsSpan(16384)):F1}Hz");
            }
            Assert.Fail($"Output {outputFreq:F1}Hz outside [200,260]; per-stage: {string.Join(" | ", stages)}");
        }
    }

    [Fact]
    public void PitchShifter_OnVoiceLikeSignal_ShiftsUp()
    {
        // Verify the measurement rig + the pitch shifter on a voice-like (multi-
        // harmonic, enveloped) input, isolated from chorus/EQ interactions.
        const float inputFreq = 180f;
        var fx = new VxPitch { IsEnabled = true, Semitones = 4f };
        var signal = AudioTestHelpers.GenerateVoiceLike(inputFreq, 32768);
        fx.Process(signal);

        double outputFreq = AudioTestHelpers.EstimateDominantFrequency(signal.AsSpan(16384));
        Assert.InRange(outputFreq, 200, 260);
    }
}