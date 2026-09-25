// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;
using Vonvert.Engine.PresetLibrary;

namespace Vonvert.App;

/// <summary>
/// Centralizes the bidirectional mapping between <see cref="VoiceProfile"/>
/// (the serialisable preset model) and the live DSP engine effect instances.
///
/// Before this coordinator existed, the mapping was interleaved with ~450 lines
/// of WPF control updates inside <c>MainWindow.Presets.ApplyPreset</c>, making
/// the preset logic impossible to unit-test and scattering the VoiceProfile ↔
/// engine contract across the UI layer.
///
/// The coordinator is WPF-free: it only touches engine objects and plain CLR
/// types.  MainWindow remains responsible for syncing toggles, sliders, and
/// labels after calling <see cref="ApplyPreset"/>.
/// </summary>
public sealed class DspParameterCoordinator
{
    private readonly VoiceEngine _engine;

    // ── DSP effects ──
    private readonly VxPitch        _pitch;
    private readonly VxReverb       _reverb;
    private readonly VxChorus       _chorus;
    private readonly VxCompressor   _comp;
    private readonly VxGate         _gate;
    private readonly VxEq           _eq;
    private readonly NoiseReductionEffect _noiseRed;
    private readonly DelayEffect    _delay;
    private readonly DeesserEffect  _deesser;
    private readonly VxRobot        _robot;
    private readonly VxDrive        _drive;
    private readonly TiltEQEffect   _tiltEq;
    private readonly GraphicEQEffect _graphicEq;
    private readonly BitcrusherEffect _bitcrusher;
    private readonly FlangerEffect  _flanger;
    private readonly PhaserEffect   _phaser;
    private readonly TremoloEffect  _tremolo;
    private readonly VibratoEffect  _vibrato;
    private readonly RingModEffect        _ringMod;
    private readonly LoFiReverbEffect     _loFiReverb;
    private readonly ModulationDelayEffect _modulationDelay;

    /// <summary>
    /// Every effect instance this coordinator reads and writes. Built from the
    /// fields themselves so <see cref="ManagedEffectNames"/> cannot drift from
    /// what is actually mapped.
    /// </summary>
    private readonly IAudioEffect[] _managed;

    /// <summary>
    /// Create a coordinator bound to the given engine and effect instances.
    /// All effect references must be non-null (use the real chain instances
    /// from <c>BindDSPEffects</c>, not the pre-init shells).
    /// </summary>
    public DspParameterCoordinator(
        VoiceEngine engine,
        VxPitch pitch, VxReverb reverb, VxChorus chorus, VxCompressor comp,
        VxGate gate, VxEq eq, NoiseReductionEffect noiseRed,
        DelayEffect delay, DeesserEffect deesser, VxRobot robot, VxDrive drive,
        TiltEQEffect tiltEq, GraphicEQEffect graphicEq, BitcrusherEffect bitcrusher,
        FlangerEffect flanger, PhaserEffect phaser, TremoloEffect tremolo, VibratoEffect vibrato,
        RingModEffect ringMod, LoFiReverbEffect loFiReverb, ModulationDelayEffect modulationDelay)
    {
        _engine  = engine;
        _pitch   = pitch;   _reverb = reverb; _chorus = chorus; _comp = comp;
        _gate    = gate;    _eq     = eq;     _noiseRed = noiseRed;
        _delay   = delay;   _deesser = deesser;
        _robot   = robot;   _drive = drive;
        _tiltEq  = tiltEq;  _graphicEq = graphicEq; _bitcrusher = bitcrusher;
        _flanger = flanger; _phaser = phaser; _tremolo = tremolo; _vibrato = vibrato;
        _ringMod = ringMod; _loFiReverb = loFiReverb; _modulationDelay = modulationDelay;

        _managed = new IAudioEffect[]
        {
            _pitch, _reverb, _chorus, _comp, _gate, _eq, _noiseRed, _delay, _deesser,
            _robot, _drive, _tiltEq, _graphicEq, _bitcrusher,
            _flanger, _phaser, _tremolo, _vibrato,
            _ringMod, _loFiReverb, _modulationDelay,
        };
    }

    /// <summary>
    /// Names of the effect instances this coordinator actually reads and writes.
    /// The wiring-invariant test uses it to prove that every effect carried by
    /// the default chain is either mapped here or explicitly exempt.
    /// </summary>
    public IReadOnlyList<string> ManagedEffectNames
    {
        get
        {
            var names = new string[_managed.Length];
            for (int i = 0; i < _managed.Length; i++) names[i] = _managed[i].Name;
            return names;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Apply: VoiceProfile → Engine
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Push every parameter from <paramref name="p"/> into the live DSP engine.
    /// After this call the engine produces audio with the preset's settings;
    /// the caller is responsible for syncing UI controls (toggles, sliders,
    /// labels) to match.
    /// </summary>
    public void ApplyPreset(VoiceProfile p)
    {
        // ── Pitch ──
        _pitch.IsEnabled = p.PitchEnabled;
        _pitch.Semitones = p.PitchOffset;

        // ── Auto-pitch register normalization (opt-in; no-op when disabled) ──
        if (_engine.Pipeline is NullAudioProcessor nap)
        {
            var norm  = nap.PitchNormalizer;
            bool  auto = p.PitchEnabled && p.AutoPitchTarget;
            norm.FallbackSemitones = p.PitchOffset;
            norm.TargetF0Hz        = p.TargetF0Hz;
            norm.Enabled            = auto;
            _pitch.AutoTargetEnabled = auto;
            _pitch.AutoSource        = auto ? (IAutoPitchSource)norm : null;
            norm.Reset();
        }

        // ── Reverb ──
        _reverb.IsEnabled = p.ReverbEnabled;
        _reverb.RoomSize  = p.RoomScale;
        _reverb.Damping   = p.ReverbAbsorb;
        _reverb.Wet       = p.ReverbMix;

        // ── EQ (parametric) ──
        _eq.IsEnabled  = p.EqEnabled;
        _eq.LowGainDb  = p.EqLowDb;
        _eq.LowFreq    = p.EqLowFreq;
        _eq.MidGainDb  = p.EqMidDb;
        _eq.MidFreq    = p.EqMidFreq;
        _eq.MidQ       = p.EqMidQ;
        _eq.HighGainDb = p.EqHighDb;
        _eq.HighFreq   = p.EqHighFreq;

        // ── Chorus ──
        _chorus.IsEnabled = p.ChorusEnabled;
        _chorus.Rate      = p.ChorusSpeed;
        _chorus.Depth     = p.ChorusDepth;
        _chorus.Mix       = p.ChorusMix;

        // ── Compressor ──
        _comp.IsEnabled   = p.CompressEnabled;
        _comp.ThresholdDb = p.CompCeilingDb;
        _comp.Ratio       = p.CompStrength;

        // ── Gate ──
        _gate.IsEnabled   = p.GateEnabled;
        _gate.ThresholdDb = p.GateFloorDb;

        // ── Gain ──
        _engine.SetGain(p.PreAmpGain);

        // ── Noise Reduction ──
        _noiseRed.IsEnabled = p.NoiseReduceEnabled;
        _noiseRed.Strength  = p.NoiseReduceStrength;
        _noiseRed.Floor     = p.NoiseReduceFloor;
        _noiseRed.PerceptualStrength = p.NoiseReducePerceptual;
        _noiseRed.SuppressionFloorDb = p.NoiseReduceSuppressionDb;

        // ── Delay ──
        _delay.IsEnabled = p.DelayEnabled;
        _delay.TimeMs    = p.DelayTimeMs;
        _delay.Feedback  = p.DelayFeedback;
        _delay.Mix       = p.DelayMix;
        _delay.Damping   = p.DelayDamping;
        _delay.PingPong  = p.DelayPingPong;

        // ── Deesser ──
        _deesser.IsEnabled   = p.DeesserEnabled;
        _deesser.Frequency   = p.DeesserFreq;
        _deesser.ThresholdDb = p.DeesserThreshDb;
        _deesser.Ratio       = p.DeesserRatio;
        _deesser.BandwidthQ  = p.DeesserQ;

        // ── Robot (ring modulation) ──
        _robot.IsEnabled = p.RobotEnabled;

        // ── Distortion / Drive ──
        _drive.IsEnabled = p.DistortEnabled;
        _drive.Drive     = p.DistortSaturation;

        // ── Tilt EQ ──
        _tiltEq.IsEnabled = p.TiltEqEnabled;
        _tiltEq.Tilt      = p.Tilt;

        // ── Graphic EQ (10 bands) ──
        _graphicEq.IsEnabled = p.GraphicEqEnabled;
        for (int b = 0; b < GraphicEQEffect.Frequencies.Length && b < p.GraphicEqGains.Length; b++)
            _graphicEq.SetBandGain(b, p.GraphicEqGains[b]);

        // ── Bitcrusher ──
        _bitcrusher.IsEnabled = p.BitcrusherEnabled;
        _bitcrusher.BitDepth  = p.BitcrusherBitDepth;
        _bitcrusher.SampleRateReduction = (int)p.BitcrusherSampleReduction;

        // ── Flanger ──
        _flanger.IsEnabled = p.FlangerEnabled;
        _flanger.Rate      = p.FlangerRate;
        _flanger.Depth     = p.FlangerDepth;
        _flanger.Feedback  = p.FlangerFeedback;
        _flanger.Mix       = p.FlangerMix;

        // ── Phaser ──
        _phaser.IsEnabled = p.PhaserEnabled;
        _phaser.Rate      = p.PhaserRate;
        _phaser.Depth     = p.PhaserDepth;
        _phaser.Feedback  = p.PhaserFeedback;
        _phaser.Mix       = p.PhaserMix;

        // ── Tremolo ──
        _tremolo.IsEnabled = p.TremoloEnabled;
        _tremolo.Rate      = p.TremoloRate;
        _tremolo.Depth     = p.TremoloDepth;

        // ── Vibrato ──
        _vibrato.IsEnabled = p.VibratoEnabled;
        _vibrato.Rate      = p.VibratoRate;
        _vibrato.Depth     = p.VibratoDepth;
        _vibrato.Mix       = p.VibratoMix;

        // ── Ring modulation / lo-fi space / warble (ported set) ──
        ApplyRingMod(p);
        ApplyLoFiReverb(p);
        ApplyModulationDelay(p);
    }

    private void ApplyRingMod(VoiceProfile p)
    {
        _ringMod.IsEnabled     = p.RingModEnabled;
        _ringMod.CarrierFreq   = p.RingModCarrierFreq;
        _ringMod.Mix           = p.RingModMix;
        _ringMod.HarmonicDepth = p.RingModHarmonicDepth;
    }

    private void ApplyLoFiReverb(VoiceProfile p)
    {
        _loFiReverb.IsEnabled  = p.LoFiReverbEnabled;
        _loFiReverb.RoomSize   = p.LoFiReverbRoomSize;
        _loFiReverb.Decay      = p.LoFiReverbDecay;
        _loFiReverb.Downsample = (int)p.LoFiReverbDownsample;
        _loFiReverb.BitCrush   = p.LoFiReverbBitCrush;
        _loFiReverb.Mix        = p.LoFiReverbMix;
    }

    private void ApplyModulationDelay(VoiceProfile p)
    {
        _modulationDelay.IsEnabled   = p.ModulationDelayEnabled;
        _modulationDelay.BaseDelayMs = p.ModDelayBaseMs;
        _modulationDelay.ModDepth    = p.ModDelayDepth;
        _modulationDelay.Feedback    = p.ModDelayFeedback;
        _modulationDelay.Mix         = p.ModDelayMix;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Capture: Engine → VoiceProfile
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Read the current state of every DSP effect and return a new
    /// <see cref="VoiceProfile"/> that reproduces it.
    ///
    /// This reads from the <b>engine</b> (source of truth for audio), not
    /// from UI controls.
    /// </summary>
    public VoiceProfile CaptureToProfile()
    {
        var p = new VoiceProfile
        {
            // Pitch
            PitchEnabled = _pitch.IsEnabled,
            PitchOffset  = _pitch.Semitones,

            // Reverb
            ReverbEnabled = _reverb.IsEnabled,
            ReverbMix     = _reverb.Wet,
            RoomScale     = _reverb.RoomSize,
            ReverbAbsorb  = _reverb.Damping,

            // EQ
            EqEnabled  = _eq.IsEnabled,
            EqLowDb    = _eq.LowGainDb,
            EqLowFreq  = _eq.LowFreq,
            EqMidDb    = _eq.MidGainDb,
            EqMidFreq  = _eq.MidFreq,
            EqMidQ     = _eq.MidQ,
            EqHighDb   = _eq.HighGainDb,
            EqHighFreq = _eq.HighFreq,

            // Chorus
            ChorusEnabled = _chorus.IsEnabled,
            ChorusSpeed   = _chorus.Rate,
            ChorusDepth   = _chorus.Depth,
            ChorusMix     = _chorus.Mix,

            // Compressor
            CompressEnabled = _comp.IsEnabled,
            CompCeilingDb   = _comp.ThresholdDb,
            CompStrength    = _comp.Ratio,

            // Gate
            GateEnabled = _gate.IsEnabled,
            GateFloorDb = _gate.ThresholdDb,

            // Noise Reduction
            NoiseReduceEnabled     = _noiseRed.IsEnabled,
            NoiseReduceStrength    = _noiseRed.Strength,
            NoiseReduceFloor       = _noiseRed.Floor,
            NoiseReducePerceptual  = _noiseRed.PerceptualStrength,
            NoiseReduceSuppressionDb = _noiseRed.SuppressionFloorDb,

            // Delay
            DelayEnabled  = _delay.IsEnabled,
            DelayTimeMs   = _delay.TimeMs,
            DelayFeedback = _delay.Feedback,
            DelayMix      = _delay.Mix,
            DelayDamping  = _delay.Damping,
            DelayPingPong = _delay.PingPong,

            // Deesser
            DeesserEnabled  = _deesser.IsEnabled,
            DeesserFreq     = _deesser.Frequency,
            DeesserThreshDb = _deesser.ThresholdDb,
            DeesserRatio    = _deesser.Ratio,
            DeesserQ        = _deesser.BandwidthQ,

            // Robot
            RobotEnabled = _robot.IsEnabled,

            // Distortion
            DistortEnabled    = _drive.IsEnabled,
            DistortSaturation = _drive.Drive,

            // Tilt EQ
            TiltEqEnabled = _tiltEq.IsEnabled,
            Tilt          = _tiltEq.Tilt,

            // Graphic EQ — clone so the returned profile never aliases the
            // live effect's mutable gain array.
            GraphicEqEnabled = _graphicEq.IsEnabled,
            GraphicEqGains   = (float[])_graphicEq.GainsDb.Clone(),

            // Bitcrusher
            BitcrusherEnabled         = _bitcrusher.IsEnabled,
            BitcrusherBitDepth        = _bitcrusher.BitDepth,
            BitcrusherSampleReduction = _bitcrusher.SampleRateReduction,

            // Flanger
            FlangerEnabled  = _flanger.IsEnabled,
            FlangerRate     = _flanger.Rate,
            FlangerDepth    = _flanger.Depth,
            FlangerFeedback = _flanger.Feedback,
            FlangerMix      = _flanger.Mix,

            // Phaser
            PhaserEnabled   = _phaser.IsEnabled,
            PhaserRate      = _phaser.Rate,
            PhaserDepth     = _phaser.Depth,
            PhaserFeedback  = _phaser.Feedback,
            PhaserMix       = _phaser.Mix,

            // Tremolo
            TremoloEnabled  = _tremolo.IsEnabled,
            TremoloRate     = _tremolo.Rate,
            TremoloDepth    = _tremolo.Depth,

            // Vibrato
            VibratoEnabled  = _vibrato.IsEnabled,
            VibratoRate     = _vibrato.Rate,
            VibratoDepth    = _vibrato.Depth,
            VibratoMix      = _vibrato.Mix,
        };

        CaptureRingMod(p);
        CaptureLoFiReverb(p);
        CaptureModulationDelay(p);

        return p;
    }

    private void CaptureRingMod(VoiceProfile p)
    {
        p.RingModEnabled       = _ringMod.IsEnabled;
        p.RingModCarrierFreq   = _ringMod.CarrierFreq;
        p.RingModMix           = _ringMod.Mix;
        p.RingModHarmonicDepth = _ringMod.HarmonicDepth;
    }

    private void CaptureLoFiReverb(VoiceProfile p)
    {
        p.LoFiReverbEnabled    = _loFiReverb.IsEnabled;
        p.LoFiReverbRoomSize   = _loFiReverb.RoomSize;
        p.LoFiReverbDecay      = _loFiReverb.Decay;
        p.LoFiReverbDownsample = _loFiReverb.Downsample;
        p.LoFiReverbBitCrush   = _loFiReverb.BitCrush;
        p.LoFiReverbMix        = _loFiReverb.Mix;
    }

    private void CaptureModulationDelay(VoiceProfile p)
    {
        p.ModulationDelayEnabled = _modulationDelay.IsEnabled;
        p.ModDelayBaseMs         = _modulationDelay.BaseDelayMs;
        p.ModDelayDepth          = _modulationDelay.ModDepth;
        p.ModDelayFeedback       = _modulationDelay.Feedback;
        p.ModDelayMix            = _modulationDelay.Mix;
    }
}
