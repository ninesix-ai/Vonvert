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

// LT-001 ~ LT-008: the loudness reference line and the peak traffic light.
//
// The meter drew one fixed line at -23 LUFS, the broadcast number, while the people this
// window is aimed at publish at -16 (podcasts) or -14 (streaming and video). Everyone
// outside broadcasting was therefore told to sit several LUFS too quiet, and could not
// turn the reference off. LT-008 additionally pins that every new copy string exists in
// all shipped languages, which is where a new meter legend usually breaks.
public sealed class LufsTargetModelTests
{
    [Fact(DisplayName = "LT-001: the default is still broadcast -23, so existing users see no change")]
    public void LT001_DefaultBroadcast()
    {
        var target = new LufsTargetModel();
        Assert.Equal(LufsTargetPreset.Broadcast, target.Preset);
        Assert.Equal(-23d, target.LUFS!.Value, 3);
        Assert.Equal("FsTargetBroadcast", target.LabelKey);
    }

    [Fact(DisplayName = "LT-002: the button cycles all four states and wraps, never a dead end")]
    public void LT002_CycleWraps()
    {
        var target = new LufsTargetModel();
        var seen = new List<LufsTargetPreset> { target.Preset };
        for (int i = 0; i < 4; i++)
        {
            target.Cycle();
            seen.Add(target.Preset);
        }
        Assert.Equal(new[]
        {
            LufsTargetPreset.Broadcast, LufsTargetPreset.Streaming, LufsTargetPreset.Podcast,
            LufsTargetPreset.Hidden, LufsTargetPreset.Broadcast,
        }, seen);
    }

    [Fact(DisplayName = "LT-003: the presets carry the numbers the guides quote for each delivery format")]
    public void LT003_StandardValues()
    {
        Assert.Equal(-23d, ValueOf(LufsTargetPreset.Broadcast));
        Assert.Equal(-16d, ValueOf(LufsTargetPreset.Podcast));
        Assert.Equal(-14d, ValueOf(LufsTargetPreset.Streaming));
        Assert.Null(ValueOfOrNull(LufsTargetPreset.Hidden));
    }

    [Fact(DisplayName = "LT-004: every preset names its own copy key, so the button can never render a bare key")]
    public void LT004_LabelKeysDistinct()
    {
        var keys = Enum.GetValues<LufsTargetPreset>()
            .Select(p => { var t = new LufsTargetModel(); t.Set(p); return t.LabelKey; })
            .ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.StartsWith("Fs", k));
    }

    [Theory(DisplayName = "LT-005: the peak alert crosses at the ceiling and never at silence")]
    [InlineData(-99, PeakAlert.Ok)]          // no signal at all
    [InlineData(-30, PeakAlert.Ok)]
    [InlineData(-1.0, PeakAlert.Ok)]         // exactly the recommended ceiling is still fine
    [InlineData(-0.9, PeakAlert.Warning)]
    [InlineData(-0.0, PeakAlert.Clip)]
    [InlineData(0.5, PeakAlert.Clip)]
    [InlineData(-999, PeakAlert.Ok)]         // meter reports silence as a very large negative
    public void LT005_PeakThresholds(double truePeak, PeakAlert expected)
        => Assert.Equal(expected, LufsPeakAlert.LevelFor(truePeak));

    [Fact(DisplayName = "LT-006: NaN and minus infinity read as ok instead of alarming the user")]
    public void LT006_InvalidReadingsAreCalm()
    {
        Assert.Equal(PeakAlert.Ok, LufsPeakAlert.LevelFor(double.NaN));
        Assert.Equal(PeakAlert.Ok, LufsPeakAlert.LevelFor(double.NegativeInfinity));
    }

    [Fact(DisplayName = "LT-007: the alert only ever escalates as the signal rises")]
    public void LT007_Monotonic()
    {
        PeakAlert previous = PeakAlert.Ok;
        for (double tp = -40; tp <= 3; tp += 0.25)
        {
            var now = LufsPeakAlert.LevelFor(tp);
            Assert.True(now >= previous, $"alert went backwards at {tp:F2} dBTP: {previous} -> {now}");
            previous = now;
        }
    }

    [Theory(DisplayName = "LT-008: every new meter string exists in every shipped language")]
    [MemberData(nameof(Languages))]
    public void LT008_AllMeterKeysAreTranslated(string lang)
    {
        var ui = ReadUiSection(lang);
        var required = new[]
        {
            "FsTargetBroadcast", "FsTargetStreaming", "FsTargetPodcast", "FsTargetNone",
            "FsTpWarning", "FsTpClip",
            "FsLegendMomentary", "FsLegendShort", "FsLegendIntegrated", "FsScaleTip",
            "FsPitchOn", "FsPitchNear", "FsPitchOff",
        };
        var missing = required.Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(), $"{lang}: missing meter keys [{string.Join(", ", missing)}]");
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static double ValueOf(LufsTargetPreset preset) => ValueOfOrNull(preset)!.Value;

    private static double? ValueOfOrNull(LufsTargetPreset preset)
    {
        var t = new LufsTargetModel();
        t.Set(preset);
        return t.LUFS;
    }

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
