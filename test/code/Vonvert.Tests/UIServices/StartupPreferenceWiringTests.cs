// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

// Both the UI language and the soundboard live-mode flag are persisted by their own
// writers, so a save-only regression is invisible until the next restart. These guards pin
// the OTHER half of the round trip - the startup read-back in App.xaml.cs - because that
// wiring lives in a file no unit test otherwise touches, and dropping either line silently
// resets the user's choice to the built-in default on every launch.
public sealed class StartupPreferenceWiringTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string AppSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "App.xaml.cs"));

    [Fact(DisplayName = "WIRE-001: startup restores the saved UI language before the main window is built")]
    public void Language_IsReloadedAtStartup()
    {
        var src = AppSource;
        var load = src.IndexOf("LoadSavedLanguage()", StringComparison.Ordinal);
        Assert.True(load >= 0, "App startup never calls LocalizationManager.LoadSavedLanguage()");

        // Reading the preference after MainWindow exists would paint the UI in the default
        // language first and only switch afterwards; the ctor is the last safe boundary.
        var window = src.IndexOf("new MainWindow(", StringComparison.Ordinal);
        Assert.True(window >= 0, "MainWindow construction not found - update this guard");
        Assert.True(load < window,
            "LoadSavedLanguage() must run before the first MainWindow is constructed");
    }

    [Fact(DisplayName = "WIRE-002: startup pushes the persisted soundboard live-mode into the manager")]
    public void SoundboardLiveMode_IsReloadedAtStartup()
    {
        var src = AppSource;
        Assert.Matches(
            @"Soundboard\s*\.\s*LiveMode\s*=\s*AppConfig\.Instance\.Audio\.SoundboardLiveMode",
            src);

        // Assigning the flag before the manager exists is a silent no-op on a null field.
        var created = src.IndexOf("new SoundboardManager(", StringComparison.Ordinal);
        var applied = Regex.Match(src, @"Soundboard\s*\.\s*LiveMode\s*=");
        Assert.True(created >= 0 && applied.Success, "soundboard wiring not found - update this guard");
        Assert.True(created < applied.Index,
            "LiveMode must be applied after SoundboardManager is constructed");
    }
}
