// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Auto-split from LocalizationManager.cs — do not edit manually.

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Dialogs & MessageBoxes ──
    public string MicDisconnected  => G();
    public string TrayOpen         => G();
    public string TrayQuit         => G();

    // ── Pitch Visualization ──
    public string CentsUnit         => G();
    public string HzUnit            => G();

    // ── Novice feedback toasts ──
    public string ToastPresetApplied => G();
    public string ToastPowerOn      => G();
    public string ToastPowerOff     => G();

    // ── Bottom control bar ──
    public string VoiceChanger      => G();
    public string VoiceToggle       => G();
    public string VuIn              => G();
    public string VuOut             => G();

    // ── Settings tab (device card) ──
    public string HearMyself        => G();

    // ── Voices tab (spectrum strip + quick-start hints) ──
    public string Spectrum          => G();
    public string PitchCurve        => G();
    public string QuickStartHint    => G();
    public string NoMicFound        => G();
    public string NoMicHint         => G();

    // ── Update badge tooltip ──
    public string UpdateBadgeTip    => G();

    // ── Accessibility ──
    public string AccMainContent    => G();
    public string AccBottomBar      => G();
    public string AccPowerToggle    => G();

    // ── Update badge ──
    public string UpdateBadgeTooltipFmt     => G();

    // ── A/B Compare segmented control (Voices tab section header) ──
    public string SegDry                    => G();
    public string SegNormal                 => G();
    public string SegWet                    => G();
    public string SegDryTip                 => G();
    public string SegNormalTip              => G();
    public string SegWetTip                 => G();
}
