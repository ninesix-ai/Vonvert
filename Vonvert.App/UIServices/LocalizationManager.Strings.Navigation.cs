// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Auto-split from LocalizationManager.cs — do not edit manually.

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── App Name ──
    public string AppName          => G();

    // ── Sidebar navigation (left icon rail) ──
    public string NavVoices        => G();
    public string NavSettings      => G();
    public string SelectVoice      => G();

    // ── Window chrome tooltips ──
    public string MinimizeTip      => G();
    public string MaximizeTip      => G();

    // ── Header / Status badges ──
    public string CloseMinTray     => G();

    // ── Status bar ──
    public string LatencyPrefix    => G();
    public string Stopped          => G();
    public string UnderrunsFmt     => G();
    public string RunningEffects   => G();
    public string StandbyPassthrough => G();
    public string LatencyDash      => G();

    // ── Engine states (for code-behind mapping) ──
    public string EngineStateStopped => G();
    public string EngineStateRunning => G();
    public string EngineStateError   => G();

    // ── Error notification prefixes ──
    public string ErrDspInit       => G();
}
