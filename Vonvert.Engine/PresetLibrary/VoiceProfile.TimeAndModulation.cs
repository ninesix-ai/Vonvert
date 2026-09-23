// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.PresetLibrary;

/// <content>
/// Time-based and voice-clarity effects (delay, de-esser, noise reduction)
/// parameter properties.
/// </content>
public partial class VoiceProfile
{
    // ── Delay / Echo ──────────────────────────────────────────────────
    public bool  DelayEnabled    { get; set; } = false;
    public float DelayTimeMs     { get; set; } = 300f;
    public float DelayFeedback   { get; set; } = 0.35f;
    public float DelayMix        { get; set; } = 0.3f;
    public float DelayDamping    { get; set; } = 0.5f;
    public bool  DelayPingPong   { get; set; } = false;
    // ── De-esser ──────────────────────────────────────────────────────
    public bool  DeesserEnabled  { get; set; } = false;
    public float DeesserFreq     { get; set; } = 6000f;
    public float DeesserThreshDb { get; set; } = -18f;
    public float DeesserRatio    { get; set; } = 4f;
    public float DeesserQ        { get; set; } = 2.0f;
    // ── Noise Reduction (spectral subtraction) ────────────────────────
    public bool  NoiseReduceEnabled { get; set; } = false;
    public float NoiseReduceStrength { get; set; } = 2.0f;
    public float NoiseReduceFloor    { get; set; } = 0.01f;
    public float NoiseReducePerceptual { get; set; } = 0.7f;
    public float NoiseReduceSuppressionDb { get; set; } = -20f;
    // ── Flanger (LFO-swept short delay with feedback) ─────────────────
    public bool  FlangerEnabled  { get; set; } = false;
    public float FlangerRate     { get; set; } = 0.5f;    // 0.1 – 10 Hz
    public float FlangerDepth    { get; set; } = 0.5f;    // 0 – 1
    public float FlangerFeedback { get; set; } = 0.5f;    // -0.95 – 0.95
    public float FlangerMix      { get; set; } = 0.5f;    // 0 – 1
    // ── Phaser (cascaded allpass sweep) ───────────────────────────────
    public bool  PhaserEnabled   { get; set; } = false;
    public float PhaserRate      { get; set; } = 1.0f;    // 0.1 – 8 Hz
    public float PhaserDepth     { get; set; } = 0.7f;    // 0 – 1
    public float PhaserFeedback  { get; set; } = 0.5f;    // -0.95 – 0.95
    public float PhaserMix       { get; set; } = 0.5f;    // 0 – 1
    // ── Tremolo (amplitude modulation) ────────────────────────────────
    public bool  TremoloEnabled  { get; set; } = false;
    public float TremoloRate     { get; set; } = 4f;      // 0.5 – 20 Hz
    public float TremoloDepth    { get; set; } = 0.5f;    // 0 – 1
    // ── Vibrato (pitch modulation via LFO-driven fractional delay) ────
    public bool  VibratoEnabled  { get; set; } = false;
    public float VibratoRate     { get; set; } = 5f;      // 0.5 – 12 Hz
    public float VibratoDepth    { get; set; } = 0.4f;    // 0 – 1
    public float VibratoMix      { get; set; } = 1.0f;    // 0 – 1
}
