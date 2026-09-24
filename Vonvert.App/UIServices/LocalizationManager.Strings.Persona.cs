// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Strongly-typed UI strings for the onboarding wizard and the "My role" settings card.
// Role/persona data labels (role names, descriptions, group headings, complexity tiers)
// are NOT here — they live in the JSON "persona" section and are read dynamically via
// GetPersonaLabel, so they stay out of the ui-vs-C#-property parity check.

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Onboarding wizard ──
    public string OnbWelcomeTitle      => G();
    public string OnbWelcomeBody       => G();
    public string OnbStepPersonaTitle  => G();
    public string OnbStepPersonaBody   => G();
    public string OnbStepDevicesTitle  => G();
    public string OnbStepDevicesBody   => G();
    public string OnbStepDoneTitle     => G();
    public string OnbStepDoneBody      => G();
    public string OnbBack              => G();
    public string OnbSkip              => G();
    public string OnbNext              => G();
    public string OnbGetStarted        => G();
    public string OnbStepProgress      => G();

    // ── Settings "My role" card ──
    public string SettingsMyPersonaTitle            => G();
    public string SettingsMyPersonaRole             => G();
    public string SettingsMyPersonaComplexity       => G();
    public string SettingsMyPersonaDowngradeConfirm => G();

    // ── First-run spotlight tour ──
    public string TourTitle   => G();
    public string TourStep1   => G();
    public string TourStep2   => G();
    public string TourStep3   => G();
    public string TourStep4   => G();
    public string TourDone    => G();
}
