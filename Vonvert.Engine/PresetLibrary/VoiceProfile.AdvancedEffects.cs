// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.PresetLibrary;

/// <content>
/// Loudness meter parameter property.
/// </content>
public partial class VoiceProfile
{
    // ── LoudnessMeter ─────────────────────────────────────────────────
    public bool  LoudnessMeterEnabled     { get; set; } = false;

    // ── Ring Modulation ───────────────────────────────────────────────
    public bool  RingModEnabled           { get; set; } = false;
    public float RingModCarrierFreq       { get; set; } = 80f;     // 10 – 2000 Hz
    public float RingModMix               { get; set; } = 0.5f;    // 0 – 1
    public float RingModHarmonicDepth     { get; set; } = 0f;      // 0 – 1

    // ── Modulation Delay (signal-envelope driven) ─────────────────────
    public bool  ModulationDelayEnabled   { get; set; } = false;
    public float ModDelayBaseMs           { get; set; } = 10f;     // 1 – 50 ms
    public float ModDelayDepth            { get; set; } = 0.5f;    // 0 – 1
    public float ModDelayFeedback         { get; set; } = 0.3f;    // 0 – 0.85
    public float ModDelayMix              { get; set; } = 0.4f;    // 0 – 1
}
