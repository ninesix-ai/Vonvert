// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Partial class: DSP chain factory.
/// Extracted from VoiceProfile.cs to keep the property declarations file slim.
/// </summary>
public partial class VoiceProfile
{
    /// <summary>
    /// Build a <see cref="DSPChain"/> from this profile's enabled effects.
    /// Effects are added in the canonical signal-flow order (matching
    /// <see cref="DSPChain.CreateDefault"/>). Only enabled effects are
    /// instantiated; disabled effects are skipped entirely.
    /// </summary>
    public DSPChain CreateDSPChain()
    {
        var chain = new DSPChain();

        // ── DC Offset (always included — safety filter) ──────────────────
        chain.Add(new DCOffsetFilter { IsEnabled = true });

        // ── Noise Gate ───────────────────────────────────────────────────
        if (GateEnabled)
        {
            chain.Add(new VxGate
            {
                IsEnabled = true,
                ThresholdDb = GateFloorDb
            });
        }

        // ── Noise Reduction ──────────────────────────────────────────────
        if (NoiseReduceEnabled)
        {
            chain.Add(new NoiseReductionEffect
            {
                IsEnabled = true,
                Strength = NoiseReduceStrength,
                Floor = NoiseReduceFloor,
                PerceptualStrength = NoiseReducePerceptual,
                SuppressionFloorDb = NoiseReduceSuppressionDb
            });
        }

        // ── Pitch Shift ──────────────────────────────────────────────────
        if (PitchEnabled)
        {
            chain.Add(new VxPitch
            {
                IsEnabled = true,
                Semitones = PitchOffset,
                TransientProtection = PitchTransientProtection
            });
        }

        // ── Deesser ──────────────────────────────────────────────────────
        if (DeesserEnabled)
        {
            chain.Add(new DeesserEffect
            {
                IsEnabled = true,
                Frequency = DeesserFreq,
                ThresholdDb = DeesserThreshDb,
                Ratio = DeesserRatio,
                BandwidthQ = DeesserQ
            });
        }

        // ── EQ (3-band parametric) ───────────────────────────────────────
        if (EqEnabled)
        {
            chain.Add(new VxEq
            {
                IsEnabled = true,
                LowGainDb = EqLowDb,
                LowFreq = EqLowFreq,
                MidGainDb = EqMidDb,
                MidFreq = EqMidFreq,
                MidQ = EqMidQ,
                HighGainDb = EqHighDb,
                HighFreq = EqHighFreq
            });
        }

        // ── Tilt EQ (single-knob tonal balance) ──────────────────────────
        if (TiltEqEnabled)
        {
            chain.Add(new TiltEQEffect
            {
                IsEnabled = true,
                Tilt = Tilt
            });
        }

        // ── Graphic EQ (10-band) ─────────────────────────────────────────
        if (GraphicEqEnabled)
        {
            var geq = new GraphicEQEffect { IsEnabled = true };
            for (int b = 0; b < GraphicEQEffect.Frequencies.Length && b < GraphicEqGains.Length; b++)
                geq.SetBandGain(b, GraphicEqGains[b]);
            chain.Add(geq);
        }

        // ── Robot (ring-modulation timbre) ───────────────────────────────
        if (RobotEnabled)
        {
            chain.Add(new VxRobot { IsEnabled = true });
        }

        // ── Ring Modulation ────────────────────────────────────────────
        if (RingModEnabled)
        {
            chain.Add(new RingModEffect
            {
                IsEnabled = true,
                CarrierFreq = RingModCarrierFreq,
                Mix = RingModMix,
                HarmonicDepth = RingModHarmonicDepth
            });
        }

        // ── Distortion / Drive ───────────────────────────────────────────
        if (DistortEnabled)
        {
            chain.Add(new VxDrive
            {
                IsEnabled = true,
                Drive = DistortSaturation
            });
        }

        // ── Bitcrusher (lo-fi bit depth / sample-rate reduction) ─────────
        if (BitcrusherEnabled)
        {
            chain.Add(new BitcrusherEffect
            {
                IsEnabled = true,
                BitDepth = BitcrusherBitDepth,
                SampleRateReduction = (int)BitcrusherSampleReduction
            });
        }

        // ── Chorus ───────────────────────────────────────────────────────
        if (ChorusEnabled)
        {
            chain.Add(new VxChorus
            {
                IsEnabled = true,
                Rate = ChorusSpeed,
                Depth = ChorusDepth,
                Mix = ChorusMix
            });
        }

        // ── Flanger ────────────────────────────────────────────────────
        if (FlangerEnabled)
        {
            chain.Add(new FlangerEffect
            {
                IsEnabled = true,
                Rate = FlangerRate,
                Depth = FlangerDepth,
                Feedback = FlangerFeedback,
                Mix = FlangerMix
            });
        }

        // ── Phaser ─────────────────────────────────────────────────────
        if (PhaserEnabled)
        {
            chain.Add(new PhaserEffect
            {
                IsEnabled = true,
                Rate = PhaserRate,
                Depth = PhaserDepth,
                Feedback = PhaserFeedback,
                Mix = PhaserMix
            });
        }

        // ── Vibrato ────────────────────────────────────────────────────
        if (VibratoEnabled)
        {
            chain.Add(new VibratoEffect
            {
                IsEnabled = true,
                Rate = VibratoRate,
                Depth = VibratoDepth,
                Mix = VibratoMix
            });
        }

        // ── Tremolo ────────────────────────────────────────────────────
        if (TremoloEnabled)
        {
            chain.Add(new TremoloEffect
            {
                IsEnabled = true,
                Rate = TremoloRate,
                Depth = TremoloDepth
            });
        }

        // ── Delay ────────────────────────────────────────────────────────
        if (DelayEnabled)
        {
            chain.Add(new DelayEffect
            {
                IsEnabled = true,
                TimeMs = DelayTimeMs,
                Feedback = DelayFeedback,
                Mix = DelayMix,
                Damping = DelayDamping,
                PingPong = DelayPingPong
            });
        }

        // ── Modulation Delay ───────────────────────────────────────────
        if (ModulationDelayEnabled)
        {
            chain.Add(new ModulationDelayEffect
            {
                IsEnabled = true,
                BaseDelayMs = ModDelayBaseMs,
                ModDepth = ModDelayDepth,
                Feedback = ModDelayFeedback,
                Mix = ModDelayMix
            });
        }

        // ── Tempo Delay (BPM-synced echo) ────────────────────────────────
        if (TempoDelayEnabled)
        {
            chain.Add(new TempoDelayEffect
            {
                IsEnabled = true,
                Bpm = TempoBpm,
                Division = (TempoNoteDivision)(int)TempoDivision,
                Feedback = TempoFeedback,
                Damping = TempoDamping,
                Mix = TempoMix
            });
        }

        // ── Reverb ───────────────────────────────────────────────────────
        if (ReverbEnabled)
        {
            chain.Add(new VxReverb
            {
                IsEnabled = true,
                RoomSize = RoomScale,
                Damping = ReverbAbsorb,
                Wet = ReverbMix
            });
        }

        // ── Lo-Fi Reverb ───────────────────────────────────────────────
        if (LoFiReverbEnabled)
        {
            chain.Add(new LoFiReverbEffect
            {
                IsEnabled = true,
                RoomSize = LoFiReverbRoomSize,
                Decay = LoFiReverbDecay,
                Downsample = (int)LoFiReverbDownsample,
                BitCrush = LoFiReverbBitCrush,
                Mix = LoFiReverbMix
            });
        }

        // ── Expander (smooth below-threshold attenuation) ────────────────
        if (ExpanderEnabled)
        {
            chain.Add(new ExpanderEffect
            {
                IsEnabled = true,
                ThresholdDb = ExpanderThresholdDb,
                Ratio = ExpanderRatio,
                AttackMs = ExpanderAttackMs,
                ReleaseMs = ExpanderReleaseMs,
                RangeDb = ExpanderRangeDb
            });
        }

        // ── Ducking Compressor (amplitude-triggered gain reduction) ──────
        if (DuckingEnabled)
        {
            chain.Add(new VxDuckingCompressor
            {
                IsEnabled = true,
                ThresholdDb = DuckThresholdDb,
                Ratio = DuckRatio,
                AttackMs = DuckAttackMs,
                ReleaseMs = DuckReleaseMs,
                RangeDb = DuckRangeDb
            });
        }

        // ── Compressor (always included if enabled — default true) ───────
        if (CompressEnabled)
        {
            chain.Add(new VxCompressor
            {
                IsEnabled = true,
                ThresholdDb = CompCeilingDb,
                Ratio = CompStrength
            });
        }

        // ── Limiter (safety stage, unconditional like the live default chain) ──
        // The live path gets this from EffectRegistry.CreateDefault() where it is
        // enabled by default; without it here, exported audio would be unbounded
        // relative to what the user monitored.
        chain.Add(new VxLimiter { IsEnabled = true });

        // ── Loudness Meter ───────────────────────────────────────────────
        if (LoudnessMeterEnabled)
        {
            chain.Add(new LoudnessMeterEffect { IsEnabled = true });
        }

        // ── VST Plugins (loaded after all built-in effects) ─────────────────
        foreach (var vst in VstPlugins)
        {
            if (!IsSafeVstPath(vst.DllPath)) continue;
            try
            {
                chain.Add(new VstEffect(vst.DllPath) { IsEnabled = vst.IsEnabled });
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "CreateDSPChain: Failed to load VST '{Path}'", vst.DllPath);
            }
        }

        // ── Free routing: apply custom effect order if specified ─────────
        if (EffectOrder.Count > 0)
        {
            chain.ReorderByName(EffectOrder);
        }

        return chain;
    }

    // ── VST path validation ──────────────────────────────────────────────────

    /// <summary>
    /// Validate that a VST plugin DLL path is safe: non-empty, no traversal
    /// sequences, ends with .dll, and exists on disk.
    /// </summary>
    internal static bool IsSafeVstPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            // Reject path traversal attempts
            if (path.Contains("..")) return false;

            var fullPath = System.IO.Path.GetFullPath(path);
            var ext = System.IO.Path.GetExtension(fullPath);

            // Must be a .dll file
            if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                return false;

            // Must exist on disk
            if (!System.IO.File.Exists(fullPath)) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
