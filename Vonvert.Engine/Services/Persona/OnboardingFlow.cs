// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.Engine.Services;

/// <summary>How the onboarding wizard ended (completed / skipped / cancelled).</summary>
public enum OnboardingOutcome { Completed, Skipped, Cancelled }

/// <summary>Maps a wizard outcome onto role state (pure logic, no persistence).
/// Completed-without-persona == skipped semantics (L1 minimal). Cancelled writes nothing
/// so the unconfigured -> Standard fallback stays intact. Caller persists via Save(section).</summary>
public static class OnboardingFlow
{
    public static void Apply(OnboardingOutcome outcome, PersonaType? chosen, RoleProfileService profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        switch (outcome)
        {
            case OnboardingOutcome.Completed when chosen is PersonaType persona:
                profile.SetPersonas(new[] { persona });
                profile.HasCompletedOnboarding = true;
                break;
            case OnboardingOutcome.Completed:
            case OnboardingOutcome.Skipped:
                profile.ResetToDefault();
                break;
            case OnboardingOutcome.Cancelled:
            default:
                break;
        }
    }
}
