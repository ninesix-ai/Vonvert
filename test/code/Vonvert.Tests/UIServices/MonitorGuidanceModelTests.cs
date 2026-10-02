// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// MG-001 ~ MG-008: which "here is what is going on" banner the monitor should show.
// Pure and WPF-free so the priority order is pinned: a novice reads a frozen black
// window as a crash, and the three ways that happen (engine off, dry bypass, dead mic)
// each need a different sentence.
public sealed class MonitorGuidanceModelTests
{
    private readonly MonitorGuidanceModel _guidance = new();

    [Fact(DisplayName = "MG-001: a running engine with fresh input shows no banner")]
    public void MG001_HealthyIsSilent()
        => Assert.Equal(GuidanceKind.None, _guidance.Evaluate(engineReady: true, analysisBypassed: false, peakLevel: 0.4, secondsSincePeak: 0.1));

    [Fact(DisplayName = "MG-002: an engine that never started outranks every other state")]
    public void MG002_EngineNotReadyWins()
    {
        Assert.Equal(GuidanceKind.EngineNotReady,
            _guidance.Evaluate(engineReady: false, analysisBypassed: true, peakLevel: 0.0, secondsSincePeak: 99));
    }

    [Fact(DisplayName = "MG-003: dry bypass (A/B on DRY or the power button off) is reported as a paused monitor, not as silence")]
    public void MG003_DryBypass()
    {
        // The engine is running and the mic is alive; only the analyzers are bypassed.
        Assert.Equal(GuidanceKind.DryBypass,
            _guidance.Evaluate(engineReady: true, analysisBypassed: true, peakLevel: 0.5, secondsSincePeak: 0.2));
    }

    [Fact(DisplayName = "MG-004: dry bypass outranks the no-input warning")]
    public void MG004_BypassBeatsNoInput()
        => Assert.Equal(GuidanceKind.DryBypass,
            _guidance.Evaluate(engineReady: true, analysisBypassed: true, peakLevel: 0.0, secondsSincePeak: 60));

    [Fact(DisplayName = "MG-005: being quiet for a moment is not a fault")]
    public void MG005_RecentPeakIsNotNoInput()
    {
        Assert.Equal(GuidanceKind.None,
            _guidance.Evaluate(engineReady: true, analysisBypassed: false, peakLevel: 0.0001, secondsSincePeak: 1.0));
    }

    [Fact(DisplayName = "MG-006: a dead input past the silence window raises the no-input hint")]
    public void MG006_NoInputAfterWindow()
    {
        Assert.Equal(GuidanceKind.NoInput,
            _guidance.Evaluate(engineReady: true, analysisBypassed: false, peakLevel: 0.0, secondsSincePeak: MonitorGuidanceModel.SilenceSeconds));
    }

    [Fact(DisplayName = "MG-007: a live signal clears the no-input hint even long after startup")]
    public void MG007_LiveSignalClears()
    {
        Assert.Equal(GuidanceKind.None,
            _guidance.Evaluate(engineReady: true, analysisBypassed: false, peakLevel: 0.02, secondsSincePeak: 300));
    }

    [Fact(DisplayName = "MG-008: the silence window is positive, so the banner cannot flicker on every frame")]
    public void MG008_SilenceWindowSane()
        => Assert.True(MonitorGuidanceModel.SilenceSeconds >= 1.0,
            $"silence window is {MonitorGuidanceModel.SilenceSeconds}s; below 1s a banner would blink between words");

    [Theory(DisplayName = "MG-009: every guidance state has copy in every shipped language")]
    [MemberData(nameof(Languages))]
    public void MG009_EveryStateIsTranslated(string lang)
    {
        var ui = ReadUiSection(lang);
        var keys = new[] { GuidanceKind.EngineNotReady, GuidanceKind.DryBypass, GuidanceKind.NoInput }
            .Select(MonitorGuidanceModel.KeyFor)
            .Append("FsErrOpenFailed")
            .Where(k => k.Length > 0)
            .ToList();

        var missing = keys.Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(),
            $"{lang}: guidance keys absent from the ui section: [{string.Join(", ", missing)}]");
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
