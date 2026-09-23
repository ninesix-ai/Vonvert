// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using Vonvert.Engine.DspEngine;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Declarative description of every user-editable <see cref="VoiceProfile"/>
/// parameter, grouped by the DSP effect that owns it.
///
/// This is the single source of truth for the "expert" parameter panel: the UI
/// is generated from <see cref="Groups"/> rather than hand-wired, and the same
/// table drives range/consistency tests. Each parameter knows how to read and
/// write itself on a VoiceProfile, plus its slider range and neutral default.
///
/// Kept free of any WPF / UI dependency so it lives in the engine and can be
/// shared across builds.
/// </summary>
public sealed class DspParam
{
    public string LabelKey { get; }
    /// <summary>Optional localization key for a hover tooltip explaining the
    /// parameter. Resolved against the JSON "params" section by the panel.</summary>
    public string? TipKey { get; }
    public float Min { get; }
    public float Max { get; }
    public float Default { get; }
    public float Step { get; }
    public bool IsBool { get; }

    // Exactly one accessor pair is populated, per IsBool.
    public Func<VoiceProfile, float>? GetF { get; }
    public Action<VoiceProfile, float>? SetF { get; }
    public Func<VoiceProfile, bool>? GetB { get; }
    public Action<VoiceProfile, bool>? SetB { get; }

    private DspParam(string labelKey, float min, float max, float def, float step,
                     Func<VoiceProfile, float>? getF, Action<VoiceProfile, float>? setF,
                     Func<VoiceProfile, bool>? getB, Action<VoiceProfile, bool>? setB, bool isBool,
                     string? tipKey)
    {
        LabelKey = labelKey; Min = min; Max = max; Default = def; Step = step; IsBool = isBool;
        TipKey = tipKey;
        GetF = getF; SetF = setF; GetB = getB; SetB = setB;
    }

    public static DspParam Range(string key, float min, float max, float def,
                                 Func<VoiceProfile, float> get, Action<VoiceProfile, float> set,
                                 float step = 0f, string? tipKey = null)
        => new(key, min, max, def, step, get, set, null, null, false, tipKey);

    public static DspParam Flag(string key,
                                Func<VoiceProfile, bool> get, Action<VoiceProfile, bool> set,
                                string? tipKey = null)
        => new(key, 0, 1, 0, 0, null, null, get, set, true, tipKey);
}

/// <summary>A group of parameters owned by one effect, optionally with an on/off switch.</summary>
public sealed class DspGroup
{
    public string NameKey { get; }
    public bool HasEnable { get; }
    /// <summary>Shown in the panel's "Simple" mode; all groups show in "Professional" mode.</summary>
    public bool Essential { get; }
    public Func<VoiceProfile, bool>? GetEnable { get; }
    public Action<VoiceProfile, bool>? SetEnable { get; }
    public IReadOnlyList<DspParam> Params { get; }

    public DspGroup(string nameKey, IReadOnlyList<DspParam> @params,
                    Func<VoiceProfile, bool>? getEnable = null, Action<VoiceProfile, bool>? setEnable = null,
                    bool essential = false)
    {
        NameKey = nameKey; Params = @params; Essential = essential;
        GetEnable = getEnable; SetEnable = setEnable; HasEnable = getEnable != null;
    }
}

public static class DspParameterCatalog
{
    public static IReadOnlyList<DspGroup> Groups { get; } = Build();

    private static IReadOnlyList<DspGroup> Build()
    {
        return new List<DspGroup>
        {
            new("GrpGain", new[]
            {
                DspParam.Range("P_PreAmpGain", 0f, 2f, 1f, p => p.PreAmpGain, (p, v) => p.PreAmpGain = v),
            }, essential: true),

            new("GrpPitch", new[]
            {
                DspParam.Range("P_PitchSemitones", -24f, 24f, 0f, p => p.PitchOffset, (p, v) => p.PitchOffset = v),
                DspParam.Flag("P_AutoPitch", p => p.AutoPitchTarget, (p, v) => p.AutoPitchTarget = v, tipKey: "P_AutoPitchTip"),
                DspParam.Range("P_TargetF0", 50f, 500f, 220f, p => p.TargetF0Hz, (p, v) => p.TargetF0Hz = v),
            }, p => p.PitchEnabled, (p, v) => p.PitchEnabled = v, essential: true),

            new("GrpEQ", new[]
            {
                DspParam.Range("P_EqLowDb", -12f, 12f, 0f, p => p.EqLowDb, (p, v) => p.EqLowDb = v),
                DspParam.Range("P_EqLowFreq", 20f, 500f, 150f, p => p.EqLowFreq, (p, v) => p.EqLowFreq = v),
                DspParam.Range("P_EqMidDb", -12f, 12f, 0f, p => p.EqMidDb, (p, v) => p.EqMidDb = v),
                DspParam.Range("P_EqMidFreq", 200f, 8000f, 4000f, p => p.EqMidFreq, (p, v) => p.EqMidFreq = v),
                DspParam.Range("P_EqMidQ", 0.1f, 10f, 1f, p => p.EqMidQ, (p, v) => p.EqMidQ = v),
                DspParam.Range("P_EqHighDb", -12f, 12f, 0f, p => p.EqHighDb, (p, v) => p.EqHighDb = v),
                DspParam.Range("P_EqHighFreq", 2000f, 20000f, 8000f, p => p.EqHighFreq, (p, v) => p.EqHighFreq = v),
            }, p => p.EqEnabled, (p, v) => p.EqEnabled = v),

            new("GrpChorus", new[]
            {
                DspParam.Range("P_ChorusRate", 0.1f, 5f, 1.6f, p => p.ChorusSpeed, (p, v) => p.ChorusSpeed = v),
                DspParam.Range("P_ChorusDepth", 0f, 1f, 0.3f, p => p.ChorusDepth, (p, v) => p.ChorusDepth = v),
                DspParam.Range("P_ChorusMix", 0f, 1f, 0.4f, p => p.ChorusMix, (p, v) => p.ChorusMix = v),
            }, p => p.ChorusEnabled, (p, v) => p.ChorusEnabled = v, essential: true),

            new("GrpCompressor", new[]
            {
                DspParam.Range("P_CompThreshold", -60f, 0f, -18f, p => p.CompCeilingDb, (p, v) => p.CompCeilingDb = v),
                DspParam.Range("P_CompRatio", 1f, 20f, 4f, p => p.CompStrength, (p, v) => p.CompStrength = v),
            }, p => p.CompressEnabled, (p, v) => p.CompressEnabled = v),

            new("GrpGate", new[]
            {
                DspParam.Range("P_GateFloor", -90f, 0f, -45f, p => p.GateFloorDb, (p, v) => p.GateFloorDb = v),
            }, p => p.GateEnabled, (p, v) => p.GateEnabled = v),

            new("GrpReverb", new[]
            {
                DspParam.Range("P_RoomSize", 0f, 1f, 0.5f, p => p.RoomScale, (p, v) => p.RoomScale = v),
                DspParam.Range("P_Damping", 0f, 1f, 0.5f, p => p.ReverbAbsorb, (p, v) => p.ReverbAbsorb = v),
                DspParam.Range("P_ReverbMix", 0f, 1f, 0.25f, p => p.ReverbMix, (p, v) => p.ReverbMix = v),
            }, p => p.ReverbEnabled, (p, v) => p.ReverbEnabled = v, essential: true),

            new("GrpDelay", new[]
            {
                DspParam.Range("P_DelayTime", 0f, 2000f, 300f, p => p.DelayTimeMs, (p, v) => p.DelayTimeMs = v),
                DspParam.Range("P_DelayFeedback", 0f, 0.95f, 0.35f, p => p.DelayFeedback, (p, v) => p.DelayFeedback = v),
                DspParam.Range("P_DelayMix", 0f, 1f, 0.3f, p => p.DelayMix, (p, v) => p.DelayMix = v),
                DspParam.Range("P_DelayDamping", 0f, 1f, 0.5f, p => p.DelayDamping, (p, v) => p.DelayDamping = v),
                DspParam.Flag("P_DelayPingPong", p => p.DelayPingPong, (p, v) => p.DelayPingPong = v),
            }, p => p.DelayEnabled, (p, v) => p.DelayEnabled = v),

            new("GrpDeesser", new[]
            {
                DspParam.Range("P_DeesserFreq", 1000f, 12000f, 6000f, p => p.DeesserFreq, (p, v) => p.DeesserFreq = v),
                DspParam.Range("P_DeesserThresh", -60f, 0f, -18f, p => p.DeesserThreshDb, (p, v) => p.DeesserThreshDb = v),
                DspParam.Range("P_DeesserRatio", 1f, 10f, 4f, p => p.DeesserRatio, (p, v) => p.DeesserRatio = v),
                DspParam.Range("P_DeesserQ", 0.1f, 10f, 2f, p => p.DeesserQ, (p, v) => p.DeesserQ = v),
            }, p => p.DeesserEnabled, (p, v) => p.DeesserEnabled = v),

            new("GrpNoiseRed", new[]
            {
                DspParam.Range("P_NRStrength", 0f, 10f, 2f, p => p.NoiseReduceStrength, (p, v) => p.NoiseReduceStrength = v),
                DspParam.Range("P_NRFloor", 0f, 1f, 0.01f, p => p.NoiseReduceFloor, (p, v) => p.NoiseReduceFloor = v),
                DspParam.Range("P_NRPerceptual", 0f, 1f, 0.7f, p => p.NoiseReducePerceptual, (p, v) => p.NoiseReducePerceptual = v),
                DspParam.Range("P_NRSuppression", -96f, 0f, -20f, p => p.NoiseReduceSuppressionDb, (p, v) => p.NoiseReduceSuppressionDb = v),
            }, p => p.NoiseReduceEnabled, (p, v) => p.NoiseReduceEnabled = v),

            new("GrpRobot", Array.Empty<DspParam>(), p => p.RobotEnabled, (p, v) => p.RobotEnabled = v, essential: true),

            new("GrpDistortion", new[]
            {
                DspParam.Range("P_Saturation", 0f, 1f, 0.3f, p => p.DistortSaturation, (p, v) => p.DistortSaturation = v),
            }, p => p.DistortEnabled, (p, v) => p.DistortEnabled = v, essential: true),

            new("GrpTiltEQ", new[]
            {
                DspParam.Range("P_Tilt", -1f, 1f, 0f, p => p.Tilt, (p, v) => p.Tilt = v),
            }, p => p.TiltEqEnabled, (p, v) => p.TiltEqEnabled = v),

            new("GrpBitcrusher", new[]
            {
                DspParam.Range("P_BitDepth", 1f, 24f, 8f, p => p.BitcrusherBitDepth, (p, v) => p.BitcrusherBitDepth = v),
                DspParam.Range("P_SRReduce", 1f, 20f, 1f, p => p.BitcrusherSampleReduction, (p, v) => p.BitcrusherSampleReduction = v),
            }, p => p.BitcrusherEnabled, (p, v) => p.BitcrusherEnabled = v),

            new("GrpFlanger", new[]
            {
                DspParam.Range("P_FlangerRate", 0.1f, 10f, 0.5f, p => p.FlangerRate, (p, v) => p.FlangerRate = v),
                DspParam.Range("P_FlangerDepth", 0f, 1f, 0.5f, p => p.FlangerDepth, (p, v) => p.FlangerDepth = v),
                DspParam.Range("P_FlangerFeedback", -0.95f, 0.95f, 0.5f, p => p.FlangerFeedback, (p, v) => p.FlangerFeedback = v),
                DspParam.Range("P_FlangerMix", 0f, 1f, 0.5f, p => p.FlangerMix, (p, v) => p.FlangerMix = v),
            }, p => p.FlangerEnabled, (p, v) => p.FlangerEnabled = v),

            new("GrpPhaser", new[]
            {
                DspParam.Range("P_PhaserRate", 0.1f, 8f, 1f, p => p.PhaserRate, (p, v) => p.PhaserRate = v),
                DspParam.Range("P_PhaserDepth", 0f, 1f, 0.7f, p => p.PhaserDepth, (p, v) => p.PhaserDepth = v),
                DspParam.Range("P_PhaserFeedback", -0.95f, 0.95f, 0.5f, p => p.PhaserFeedback, (p, v) => p.PhaserFeedback = v),
                DspParam.Range("P_PhaserMix", 0f, 1f, 0.5f, p => p.PhaserMix, (p, v) => p.PhaserMix = v),
            }, p => p.PhaserEnabled, (p, v) => p.PhaserEnabled = v),

            new("GrpVibrato", new[]
            {
                DspParam.Range("P_VibratoRate", 0.5f, 12f, 5f, p => p.VibratoRate, (p, v) => p.VibratoRate = v),
                DspParam.Range("P_VibratoDepth", 0f, 1f, 0.4f, p => p.VibratoDepth, (p, v) => p.VibratoDepth = v),
                DspParam.Range("P_VibratoMix", 0f, 1f, 1f, p => p.VibratoMix, (p, v) => p.VibratoMix = v),
            }, p => p.VibratoEnabled, (p, v) => p.VibratoEnabled = v),

            new("GrpTremolo", new[]
            {
                DspParam.Range("P_TremoloRate", 0.5f, 20f, 4f, p => p.TremoloRate, (p, v) => p.TremoloRate = v),
                DspParam.Range("P_TremoloDepth", 0f, 1f, 0.5f, p => p.TremoloDepth, (p, v) => p.TremoloDepth = v),
            }, p => p.TremoloEnabled, (p, v) => p.TremoloEnabled = v),

            GraphicEqGroup(),
        };
    }

    // The 10-band graphic EQ is a fixed set of gain sliders indexed into the profile array.
    private static DspGroup GraphicEqGroup()
    {
        var bands = new List<DspParam>();
        var freqs = GraphicEQEffect.Frequencies;
        for (int i = 0; i < freqs.Length; i++)
        {
            int idx = i;
            bands.Add(DspParam.Range($"{(int)freqs[i]} Hz", -12f, 12f, 0f,
                p => idx < p.GraphicEqGains.Length ? p.GraphicEqGains[idx] : 0f,
                (p, v) => { if (idx < p.GraphicEqGains.Length) p.GraphicEqGains[idx] = v; }));
        }
        return new DspGroup("GrpGraphicEQ", bands, p => p.GraphicEqEnabled, (p, v) => p.GraphicEqEnabled = v);
    }
}
