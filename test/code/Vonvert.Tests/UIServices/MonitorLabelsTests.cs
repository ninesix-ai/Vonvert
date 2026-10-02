// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// LB-001 ~ LB-006: the plain-word / technical label switch of the fullscreen monitor.
// Plain is the default because the window ships terms (Spectrogram, LUFS, Dry, Wet)
// that a first-time streamer cannot decode; the technical name must stay visible as a
// sub-label so nobody loses the professional vocabulary either.
public sealed class MonitorLabelsTests
{
    private static readonly MonitorLabelSlot[] AllSlots =
    {
        MonitorLabelSlot.Spectrogram, MonitorLabelSlot.Waveform, MonitorLabelSlot.Loudness,
        MonitorLabelSlot.Dry, MonitorLabelSlot.Wet,
    };

    [Fact(DisplayName = "LB-001: labels default to plain words, not to jargon")]
    public void LB001_DefaultIsPlain()
        => Assert.Equal(MonitorLabelMode.Plain, new MonitorLabels().Mode);

    [Fact(DisplayName = "LB-002: toggling alternates and two toggles come back to plain")]
    public void LB002_ToggleAlternates()
    {
        var labels = new MonitorLabels();
        labels.Toggle();
        Assert.Equal(MonitorLabelMode.Technical, labels.Mode);
        labels.Toggle();
        Assert.Equal(MonitorLabelMode.Plain, labels.Mode);
    }

    [Fact(DisplayName = "LB-003: every slot resolves to the paired key in each mode")]
    public void LB003_KeyForBothModes()
    {
        var plain = new MonitorLabels();
        var tech = new MonitorLabels();
        tech.Toggle();

        Assert.Equal("FsSpectrogramPlain", plain.KeyFor(MonitorLabelSlot.Spectrogram));
        Assert.Equal("FsSpectrogram", tech.KeyFor(MonitorLabelSlot.Spectrogram));
        Assert.Equal("FsWaveformPlain", plain.KeyFor(MonitorLabelSlot.Waveform));
        Assert.Equal("FsWaveform", tech.KeyFor(MonitorLabelSlot.Waveform));
        Assert.Equal("FsLoudnessPlain", plain.KeyFor(MonitorLabelSlot.Loudness));
        Assert.Equal("FsLoudness", tech.KeyFor(MonitorLabelSlot.Loudness));
        Assert.Equal("FsDryPlain", plain.KeyFor(MonitorLabelSlot.Dry));
        Assert.Equal("FsDry", tech.KeyFor(MonitorLabelSlot.Dry));
        Assert.Equal("FsWetPlain", plain.KeyFor(MonitorLabelSlot.Wet));
        Assert.Equal("FsWet", tech.KeyFor(MonitorLabelSlot.Wet));
    }

    [Fact(DisplayName = "LB-004: plain mode keeps the technical name as a sub-label, technical mode never repeats it")]
    public void LB004_SubLabelRule()
    {
        var plain = new MonitorLabels();
        Assert.Equal("FsSpectrogram", plain.SubKeyFor(MonitorLabelSlot.Spectrogram));

        var tech = new MonitorLabels();
        tech.Toggle();
        Assert.Null(tech.SubKeyFor(MonitorLabelSlot.Spectrogram));   // would print it twice
    }

    [Fact(DisplayName = "LB-005: plain keys are distinct per slot, so no two panels share a label")]
    public void LB005_PlainKeysAreDistinct()
    {
        var labels = new MonitorLabels();
        var keys = AllSlots.Select(labels.KeyFor).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Theory(DisplayName = "LB-006: every key the switch can emit exists in every shipped language")]
    [MemberData(nameof(Languages))]
    public void LB006_EveryEmittedKeyIsTranslated(string lang)
    {
        var ui = ReadUiSection(lang);
        var bothModes = new[] { new MonitorLabels(), new MonitorLabels() };
        bothModes[1].Toggle();

        var emitted = new List<string>();
        foreach (var labels in bothModes)
            foreach (var slot in AllSlots)
            {
                emitted.Add(labels.KeyFor(slot));
                var sub = labels.SubKeyFor(slot);
                if (sub is not null) emitted.Add(sub);
            }

        var missing = emitted.Where(k => !ui.Contains(k)).Distinct();
        Assert.True(!missing.Any(),
            $"{lang}: monitor label keys absent from the ui section: [{string.Join(", ", missing)}]");
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
