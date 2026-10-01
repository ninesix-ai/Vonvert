// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.PresetLibrary;

/// <content>
/// Second-batch ported effects (tempo-synced delay, expander, ducking
/// compressor) parameter properties.
/// </content>
public partial class VoiceProfile
{
    // ── Tempo Delay (BPM-synced echo) ─────────────────────────────────────
    public bool  TempoDelayEnabled   { get; set; } = false;
    public float TempoBpm            { get; set; } = 120f;   // 30 – 300 BPM
    // Stored as the underlying value of TempoNoteDivision (0 = whole .. 10 = sixteenth).
    // Kept as a float so the stepped expert-panel slider behaves like the other
    // discrete parameters (e.g. Lo-Fi downsample); the effect casts to the enum.
    public float TempoDivision       { get; set; } = 7f;      // default: Eighth
    public float TempoFeedback       { get; set; } = 0.4f;   // 0 – 0.9
    public float TempoDamping        { get; set; } = 0.3f;   // 0 – 1
    public float TempoMix            { get; set; } = 0.35f;  // 0 – 1

    // ── Expander (smooth below-threshold attenuation) ─────────────────────
    public bool  ExpanderEnabled     { get; set; } = false;
    public float ExpanderThresholdDb { get; set; } = -30f;   // -80 – 0 dB
    public float ExpanderRatio       { get; set; } = 2f;     // 1 – 20
    public float ExpanderAttackMs    { get; set; } = 1f;     // 0.1 – 50 ms
    public float ExpanderReleaseMs   { get; set; } = 50f;    // 1 – 1000 ms
    public float ExpanderRangeDb     { get; set; } = -40f;   // -80 – 0 dB

    // ── Ducking Compressor (amplitude-triggered gain reduction) ───────────
    public bool  DuckingEnabled      { get; set; } = false;
    public float DuckThresholdDb     { get; set; } = -20f;   // -60 – 0 dB
    public float DuckRatio           { get; set; } = 4f;     // 1 – 20
    public float DuckAttackMs        { get; set; } = 2f;     // 0.5 – 50 ms
    public float DuckReleaseMs       { get; set; } = 200f;   // 50 – 2000 ms
    public float DuckRangeDb         { get; set; } = -20f;   // -60 – 0 dB
}
