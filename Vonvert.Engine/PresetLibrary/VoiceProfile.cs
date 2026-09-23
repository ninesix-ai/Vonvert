// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.DspEngine;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Voice profile with finer-grained EQ / chorus / compressor parameters.
/// </summary>
/// <remarks>
/// Properties are split across multiple partial-class files by effect category:
/// <list type="bullet">
///   <item><c>VoiceProfile.cs</c> — core metadata + basic DSP (pitch, reverb, EQ, …)</item>
///   <item><c>VoiceProfile.DspChain.cs</c> — CreateDSPChain</item>
///   <item><c>VoiceProfile.TimeAndModulation.cs</c> — delay, de-esser, noise reduction</item>
///   <item><c>VoiceProfile.AdvancedEffects.cs</c> — loudness meter</item>
/// </list>
/// </remarks>
public partial class VoiceProfile
{
    public string Name            { get; set; } = "Custom";
    public string Icon            { get; set; } = "🎙";
    // Cover art for the preset tile. Supports a relative file path or an embedded
    // data URI (e.g. "data:image/png;base64,...") so a single .vopreset file can
    // travel with its cover art. No picker UI yet — the value is preserved
    // through export/import round-trips.
    public string CoverImage      { get; set; } = "";
    // Pitch
    public bool  PitchEnabled    { get; set; } = false;
    public float PitchOffset     { get; set; } = 0f;
    public bool  PitchTransientProtection { get; set; } = false;
    // Auto-pitch register normalization (opt-in; drives VxPitch from the F0 analyzer loop)
    public bool  AutoPitchTarget { get; set; } = false;
    public float TargetF0Hz      { get; set; } = 220f;
    // Reverb
    public bool  ReverbEnabled   { get; set; } = false;
    public float RoomScale       { get; set; } = 0.5f;
    public float ReverbAbsorb    { get; set; } = 0.5f;
    public float ReverbMix       { get; set; } = 0.25f;
    // Robot (ring-modulation timbre)
    public bool  RobotEnabled    { get; set; } = false;
    // EQ (3-band: Low Shelf + Mid Peaking + High Shelf)
    public bool  EqEnabled       { get; set; } = false;
    public float EqLowDb         { get; set; } = 0f;
    public float EqLowFreq       { get; set; } = 150f;
    public float EqMidDb         { get; set; } = 0f;
    public float EqMidFreq       { get; set; } = 4000f;
    public float EqMidQ          { get; set; } = 1.0f;
    public float EqHighDb        { get; set; } = 0f;
    public float EqHighFreq      { get; set; } = 8000f;
    // Distortion (asymmetric soft-clip drive)
    public bool  DistortEnabled    { get; set; } = false;
    public float DistortSaturation { get; set; } = 0.3f;
    // Tilt EQ (single-knob bass/treble balance around a fixed pivot)
    public bool  TiltEqEnabled   { get; set; } = false;
    public float Tilt            { get; set; } = 0f;   // -1 (warm) .. +1 (bright)
    // Graphic EQ (10-band, ±12 dB per band). PresetValueGuard neutralises
    // any NaN/out-of-range element to 0 dB (the flat-gain identity).
    public bool   GraphicEqEnabled { get; set; } = false;
    public float[] GraphicEqGains  { get; set; } = new float[10];
    // Bitcrusher (lo-fi bit-depth / sample-rate reduction)
    public bool  BitcrusherEnabled         { get; set; } = false;
    public float BitcrusherBitDepth        { get; set; } = 8f;   // 1 .. 24
    public float BitcrusherSampleReduction { get; set; } = 1f;   // 1 .. 20
    // Chorus
    public bool  ChorusEnabled   { get; set; } = false;
    public float ChorusSpeed     { get; set; } = 1.6f;
    public float ChorusDepth     { get; set; } = 0.3f;
    public float ChorusMix       { get; set; } = 0.4f;   // wet/dry 0–1
    // Compressor
    public bool  CompressEnabled  { get; set; } = true;
    public float CompCeilingDb   { get; set; } = -18f;
    public float CompStrength    { get; set; } = 4f;
    // Noise Gate
    public bool  GateEnabled      { get; set; } = true;
    public float GateFloorDb     { get; set; } = -45f;
    // Gain
    public float PreAmpGain      { get; set; } = 1.0f;
    // DSP chain effect order (free routing). Empty = default order.
    public List<string> EffectOrder { get; set; } = new();

    /// <summary>
    /// Deep-enough copy for independent editing: value fields are copied by the
    /// shallow clone, and the mutable array/list fields are duplicated so an
    /// edited working copy never aliases the source preset's buffers.
    /// </summary>
    public VoiceProfile Clone()
    {
        var copy = (VoiceProfile)MemberwiseClone();
        copy.GraphicEqGains = (float[])GraphicEqGains.Clone();
        copy.EffectOrder = new List<string>(EffectOrder);
        return copy;
    }
}
