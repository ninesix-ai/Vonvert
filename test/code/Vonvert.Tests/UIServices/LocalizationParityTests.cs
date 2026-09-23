// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the bilingual (en/zh) contract for the minimal GitHub build:
//   * zh.json and en.json must expose identical ui key sets and preset key sets,
//     so a new/renamed string can never be added to one language only (a future
//     runtime fallback silently hiding an untranslated label is the bug class).
//   * DetectSystemLanguage must only ever return a supported code.
// Reads the same embedded resources the app loads at runtime.

namespace Vonvert.Tests.UIServices;

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vonvert.App.UIServices;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class LocalizationParityTests
{
    private static readonly System.Reflection.Assembly AppAsm = typeof(LocalizationManager).Assembly;

    private static HashSet<string> ReadTopLevel(string lang, string section)
    {
        using var stream = AppAsm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty(section, out var obj))
            foreach (var prop in obj.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }

    [Fact(DisplayName = "L10N-01: en.json and zh.json ui key sets are identical")]
    public void UiKeys_ParityBetweenEnAndZh()
    {
        var en = ReadTopLevel("en", "ui");
        var zh = ReadTopLevel("zh", "ui");
        Assert.True(en.SetEquals(zh),
            $"ui key drift — only-en: [{string.Join(",", en.Except(zh))}], only-zh: [{string.Join(",", zh.Except(en))}]");
        Assert.NotEmpty(en);
    }

    [Fact(DisplayName = "L10N-02: en.json and zh.json preset key sets are identical")]
    public void PresetKeys_ParityBetweenEnAndZh()
    {
        var en = ReadTopLevel("en", "presets");
        var zh = ReadTopLevel("zh", "presets");
        Assert.True(en.SetEquals(zh),
            $"preset key drift — only-en: [{string.Join(",", en.Except(zh))}], only-zh: [{string.Join(",", zh.Except(en))}]");
    }

    [Fact(DisplayName = "L10N-03: DetectSystemLanguage only ever returns a supported code")]
    public void DetectSystemLanguage_IsAlwaysSupported()
    {
        var lang = LocalizationManager.DetectSystemLanguage();
        Assert.Contains(lang, LocalizationManager.SupportedLanguages);
    }

    [Fact(DisplayName = "L10N-04: a language switch notifies the bound PTT-mode label and its text follows the language")]
    public void PttHoldModeLabel_RefreshesOnLanguageChange()
    {
        // Guards the fix's mechanism: the PTT-mode button is XAML-bound to
        // PttHoldModeLabel, so a language change must (a) raise PropertyChanged for it
        // and (b) yield a different (translated) string. This is what imperative
        // re-assignment used to get wrong (button stranded in the old language).
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        var raised = new HashSet<string>();
        PropertyChangedEventHandler handler = (_, e) => { if (e.PropertyName is not null) raised.Add(e.PropertyName); };
        lm.PropertyChanged += handler;
        try
        {
            lm.Language = "en";
            string en = lm.PttHoldModeLabel;

            raised.Clear();
            lm.Language = "zh";
            string zh = lm.PttHoldModeLabel;

            Assert.Contains(nameof(LocalizationManager.PttHoldModeLabel), raised);
            Assert.NotEqual(en, zh);
        }
        finally
        {
            lm.Language = original;
            lm.PropertyChanged -= handler;
        }
    }

    [Fact(DisplayName = "L10N-05: en.json and zh.json params key sets are identical")]
    public void ParamKeys_ParityBetweenEnAndZh()
    {
        var en = ReadTopLevel("en", "params");
        var zh = ReadTopLevel("zh", "params");
        Assert.True(en.SetEquals(zh),
            $"params key drift — only-en: [{string.Join(",", en.Except(zh))}], only-zh: [{string.Join(",", zh.Except(en))}]");
        Assert.NotEmpty(en);
    }

    [Fact(DisplayName = "L10N-06: every translatable catalog label (Grp*/P_*) and tooltip has an en params entry")]
    public void CatalogLabels_AreTranslated()
    {
        var enParams = ReadTopLevel("en", "params");
        var allParams = DspParameterCatalog.Groups.SelectMany(g => g.Params).ToList();
        var needed = DspParameterCatalog.Groups
            .Select(g => g.NameKey)
            .Concat(allParams.Select(p => p.LabelKey))
            .Concat(allParams.Select(p => p.TipKey))                 // hover tooltips must be translated too
            .Where(k => k is not null && (k.StartsWith("Grp") || k.StartsWith("P_")))   // band "80 Hz" keys are language-neutral
            .Cast<string>();
        foreach (var key in needed)
            Assert.True(enParams.Contains(key), $"catalog label '{key}' has no params translation entry");
    }
}
