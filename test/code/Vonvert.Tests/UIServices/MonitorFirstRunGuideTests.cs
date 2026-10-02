// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// FR-001 ~ FR-004: the monitor's first-run walkthrough. The guide reuses the main
// window's spotlight control, which is exactly the risk these tests cover: two tours
// sharing one marker file means finishing one silently disables the other.
public sealed class MonitorFirstRunGuideTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }

    private static string SourceOf(string relative)
        => File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", relative));

    [Fact(DisplayName = "FR-001: the monitor guide has its own marker, never the main tour's or the wizard's")]
    public void FR001_MarkerIsIndependent()
    {
        var tour = SourceOf(Path.Combine("Controls", "TourOverlay.xaml.cs"));
        var wizard = SourceOf("MainWindow.Onboarding.cs");
        var mine = $"\"{MonitorFirstRunGuide.MarkerFileName}\"";

        // The two existing one-time markers must still be there (this guard is only
        // meaningful while they are), and neither may be the one the monitor writes.
        Assert.Contains("\"tour_done\"", tour);
        Assert.Contains("\"onboarding_done\"", wizard);
        Assert.DoesNotContain(mine, tour);
        Assert.DoesNotContain(mine, wizard);
        Assert.EndsWith(MonitorFirstRunGuide.MarkerFileName, MonitorFirstRunGuide.MarkerPath);
    }

    [Fact(DisplayName = "FR-002: the guide shows only while its marker is absent")]
    public void FR002_ShowsOnce()
    {
        Assert.True(MonitorFirstRunGuide.ShouldShow(markerFileExists: false));
        Assert.False(MonitorFirstRunGuide.ShouldShow(markerFileExists: true));
    }

    [Fact(DisplayName = "FR-003: four distinct stops, matching the four things a novice must find")]
    public void FR003_StepsAreDistinct()
    {
        Assert.Equal(4, MonitorFirstRunGuide.StepKeys.Length);
        Assert.Equal(4, MonitorFirstRunGuide.StepKeys.Distinct().Count());
    }

    [Theory(DisplayName = "FR-004: every walkthrough line exists in every shipped language")]
    [MemberData(nameof(Languages))]
    public void FR004_EveryStepIsTranslated(string lang)
    {
        var ui = ReadUiSection(lang);
        var missing = MonitorFirstRunGuide.StepKeys
            .Append("FsTourTitle")
            .Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(),
            $"{lang}: walkthrough keys absent from the ui section: [{string.Join(", ", missing)}]");
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
