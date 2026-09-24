// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using Vonvert.Engine.Services;

namespace Vonvert.App.OnboardingSteps;

/// <summary>
/// Pure selection state for the onboarding role picker (no WPF dependency, so it can be
/// unit-tested). Tracks the single selected persona and raises <see cref="Changed"/> only
/// when the selection actually changes, driving both the visual highlight and the
/// value handed back to the wizard.
/// </summary>
public sealed class PersonaSelection
{
    public PersonaType? Selected { get; private set; }

    /// <summary>Raised with the new selection whenever it changes.</summary>
    public event Action<PersonaType?>? Changed;

    /// <summary>Select a persona (null clears). Returns true if the selection changed.</summary>
    public bool Select(PersonaType? persona)
    {
        if (Equals(Selected, persona)) return false;
        Selected = persona;
        Changed?.Invoke(persona);
        return true;
    }

    public bool IsSelected(PersonaType? persona) => Equals(Selected, persona);
}
