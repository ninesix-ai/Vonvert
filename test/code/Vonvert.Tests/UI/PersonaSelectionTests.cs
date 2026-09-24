// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using Vonvert.App.OnboardingSteps;
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.UI;

public class PersonaSelectionTests
{
    [Fact]
    public void Initial_Is_None()
    {
        var s = new PersonaSelection();
        Assert.Null(s.Selected);
        Assert.False(s.IsSelected(PersonaType.Gamer));
    }

    [Fact]
    public void Select_Changes_Value_And_Raises_Changed()
    {
        var s = new PersonaSelection();
        PersonaType? raised = null;
        int count = 0;
        s.Changed += p => { raised = p; count++; };

        Assert.True(s.Select(PersonaType.Gamer));
        Assert.Equal(PersonaType.Gamer, s.Selected);
        Assert.Equal(PersonaType.Gamer, raised);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ReSelect_Same_Persona_Is_Noop()
    {
        var s = new PersonaSelection();
        int count = 0;
        s.Changed += _ => count++;
        Assert.True(s.Select(PersonaType.Creator));
        Assert.False(s.Select(PersonaType.Creator));   // no change
        Assert.Equal(1, count);
    }

    [Fact]
    public void Select_Null_Clears_Selection()
    {
        var s = new PersonaSelection();
        s.Select(PersonaType.Streamer);
        Assert.True(s.Select(null));
        Assert.Null(s.Selected);
        Assert.True(s.IsSelected(null));
    }

    [Fact]
    public void IsSelected_Tracks_Current()
    {
        var s = new PersonaSelection();
        s.Select(PersonaType.Gamer);
        Assert.True(s.IsSelected(PersonaType.Gamer));
        Assert.False(s.IsSelected(PersonaType.Streamer));
    }
}
