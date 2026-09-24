// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.UI;

public class OnboardingFlowTests
{
    [Fact]
    public void Completed_With_Persona_Selects_It_And_Marks_Done()
    {
        var s = RoleProfileService.CreateForTests();
        OnboardingFlow.Apply(OnboardingOutcome.Completed, PersonaType.Gamer, s);
        Assert.Equal(new[] { PersonaType.Gamer }, s.ActivePersonas);
        Assert.True(s.HasCompletedOnboarding);
    }

    [Fact]
    public void Completed_Without_Persona_Resets_To_Default()
    {
        var s = RoleProfileService.CreateForTests();
        OnboardingFlow.Apply(OnboardingOutcome.Completed, null, s);
        Assert.Empty(s.ActivePersonas);
        Assert.Equal(ComplexityLevel.Minimal, s.EffectiveComplexity);
    }

    [Fact]
    public void Skipped_Resets_To_Default()
    {
        var s = RoleProfileService.CreateForTests();
        OnboardingFlow.Apply(OnboardingOutcome.Skipped, PersonaType.Creator, s);
        Assert.Empty(s.ActivePersonas);
        Assert.True(s.HasCompletedOnboarding);
    }

    [Fact]
    public void Cancelled_Writes_Nothing()
    {
        var s = RoleProfileService.CreateForTests();
        OnboardingFlow.Apply(OnboardingOutcome.Cancelled, PersonaType.Creator, s);
        Assert.Empty(s.ActivePersonas);
        Assert.False(s.HasCompletedOnboarding);
    }
}
