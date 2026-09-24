// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System.ComponentModel;
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.Services;

public class RoleProfileServiceTests
{
    private static RoleProfileService New() => RoleProfileService.CreateForTests();

    [Fact]
    public void Unconfigured_Defaults_To_Standard()
    {
        var s = New();
        Assert.Equal(ComplexityLevel.Standard, s.EffectiveComplexity);
        Assert.False(s.IsConfigured);
    }

    [Fact]
    public void EffectiveComplexity_Is_Max_Of_Active_Personas()
    {
        var s = New();
        s.SetPersonas(new[] { PersonaType.Beginner, PersonaType.Creator }); // Minimal + Advanced
        Assert.Equal(ComplexityLevel.Advanced, s.EffectiveComplexity);
    }

    [Fact]
    public void ManualOverride_Beats_Personas()
    {
        var s = New();
        s.SetPersonas(new[] { PersonaType.Creator });            // Advanced
        s.SetManualOverride(ComplexityLevel.Minimal);            // downgrade wins
        Assert.Equal(ComplexityLevel.Minimal, s.EffectiveComplexity);
    }

    [Fact]
    public void ResetToDefault_Clears_Personas_Minimal_Completes()
    {
        var s = New();
        s.SetPersonas(new[] { PersonaType.Gamer });
        s.ResetToDefault();
        Assert.Empty(s.ActivePersonas);
        Assert.Equal(ComplexityLevel.Minimal, s.EffectiveComplexity);
        Assert.True(s.HasCompletedOnboarding);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var src = New();
        src.SetPersonas(new[] { PersonaType.Streamer, PersonaType.Creator });
        src.SetManualOverride(ComplexityLevel.Advanced);
        var section = new AppConfig.PersonaSection();
        src.Save(section);

        var dst = New();
        dst.Load(section);
        Assert.Equal(new[] { PersonaType.Streamer, PersonaType.Creator }, dst.ActivePersonas);
        Assert.Equal(ComplexityLevel.Advanced, dst.ManualOverride);
    }

    [Fact]
    public void Load_Skips_Unknown_Persona_And_Bad_Complexity()
    {
        var section = new AppConfig.PersonaSection { ActivePersonas = new() { "Gamer", "Ghost", "" }, ComplexityLevelOverride = "Nope" };
        var s = New();
        s.Load(section);
        Assert.Equal(new[] { PersonaType.Gamer }, s.ActivePersonas);
        Assert.Null(s.ManualOverride);
    }

    [Fact]
    public void SetPersonas_Raises_Derived_PropertyChanged()
    {
        var s = New();
        var raised = false;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RoleProfileService.EffectiveComplexity)) raised = true; };
        s.SetPersonas(new[] { PersonaType.Creator });
        Assert.True(raised);
    }
}
