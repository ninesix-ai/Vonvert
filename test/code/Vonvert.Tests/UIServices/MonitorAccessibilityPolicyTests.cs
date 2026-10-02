// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// AC-001 ~ AC-006: honouring the operating system's accessibility signal.
//
// Windows already knows the user asked for high contrast, and until now the monitor ignored
// it: a low-vision user got the same translucent ink as everyone else and, three seconds
// after moving the mouse, had the pointer taken away from them. Both are decided in a pure
// function so the precedence is pinned - the user's own explicit choice must still beat the
// system signal, or the profile button would be a lie that gets overwritten on the next open.
public sealed class MonitorAccessibilityPolicyTests
{
    [Fact(DisplayName = "AC-001: a system high-contrast setting opens the monitor in the high-contrast palette")]
    public void AC001_SystemHighContrastIsHonoured()
    {
        Assert.Equal(MonitorVisualProfile.HighContrast, MonitorAccessibilityPolicy.InitialProfile(
            systemHighContrast: true, stored: MonitorVisualProfile.Brand, userChoseProfile: false));
    }

    [Fact(DisplayName = "AC-002: a palette the user picked on purpose is never overwritten by the system signal")]
    public void AC002_ExplicitChoiceWins()
    {
        Assert.Equal(MonitorVisualProfile.Chroma, MonitorAccessibilityPolicy.InitialProfile(
            systemHighContrast: true, stored: MonitorVisualProfile.Chroma, userChoseProfile: true));
    }

    [Fact(DisplayName = "AC-003: without the system signal the stored choice is used as-is")]
    public void AC003_NoSignalKeepsStored()
    {
        Assert.Equal(MonitorVisualProfile.Neutral, MonitorAccessibilityPolicy.InitialProfile(
            systemHighContrast: false, stored: MonitorVisualProfile.Neutral, userChoseProfile: true));
        Assert.Equal(MonitorVisualProfile.Brand, MonitorAccessibilityPolicy.InitialProfile(
            systemHighContrast: false, stored: MonitorVisualProfile.Brand, userChoseProfile: false));
    }

    [Fact(DisplayName = "AC-004: an untouched first run with high contrast off changes nothing on screen")]
    public void AC004_DefaultPathIsUntouched()
    {
        // The whole point of gating on userChoseProfile: nobody's monitor should look
        // different after an update unless the system told them it should.
        Assert.Equal(MonitorVisualProfile.Brand, MonitorAccessibilityPolicy.InitialProfile(
            systemHighContrast: false, stored: MonitorVisualProfile.Brand, userChoseProfile: false));
    }

    [Theory(DisplayName = "AC-005: the pointer is only hidden for people who did not ask for help seeing it")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AC005_CursorHidingRespectsHighContrast(bool systemHighContrast, bool expectHide)
        => Assert.Equal(expectHide, MonitorAccessibilityPolicy.ShouldHideCursorWhenIdle(systemHighContrast));

    [Fact(DisplayName = "AC-006: the idle beat asks the policy before taking the pointer away")]
    public void AC006_IdleHandlerConsultsThePolicy()
    {
        // A guard, not a behaviour test: the hiding happens on a timer in WPF, so the only way
        // to prove the policy is wired in is to read the code that schedules it.
        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
            RepoRoot(), "Vonvert.App", "Windows", "FullscreenVisualizationWindow.xaml.cs"));
        var idle = System.Text.RegularExpressions.Regex.Match(
            source, @"_idleTimer\.Tick[\s\S]*?\n        \};");
        Assert.True(idle.Success, "the idle beat is not where this guard expects - update it");
        Assert.Contains("ShouldHideCursorWhenIdle", idle.Value);
        Assert.Contains("HighContrast", idle.Value);
    }

    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
