// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression coverage for the "Role/Persona card does not change on language switch"
// bug (root cause: persona labels are built imperatively and must be rebuilt by
// listeners of LocalizationManager.PropertyChanged("Language")). These tests do not
// construct WPF controls; they assert the exact contract such a listener relies on:
//   * switching Language makes GetPersonaLabel return the shipped target-language value
//     (data path wired: embedded persona section actually loads per language);
//   * the "Language" PropertyChanged event fires, and by the time it fires the persona
//     dictionary is ALREADY the new language (so a self-subscribed control that rebuilds
//     in its handler can never read stale labels).

namespace Vonvert.Tests.UIServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vonvert.App.UIServices;
using Xunit;

[Collection("LocalizationManagerSingleton")]
public class PersonaLocalizationRefreshTests
{
    private static readonly System.Reflection.Assembly AppAsm = typeof(LocalizationManager).Assembly;

    // persona keys every language must resolve to a non-English, target-script value
    private const string RoleKey = "PersonaBeginner";       // "Beginner" in en
    private const string GroupKey = "PersonaGroupEntertainment";

    private static Dictionary<string, string> EmbeddedPersona(string lang)
    {
        return EmbeddedSection(lang, "persona");
    }

    private static Dictionary<string, string> EmbeddedSection(string lang, string section)
    {
        using var s = AppAsm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new InvalidOperationException($"embedded {lang}.json missing");
        using var doc = JsonDocument.Parse(s);
        var map = new Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty(section, out var p))
            foreach (var prop in p.EnumerateObject())
                map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }

    // The persona/role CARD and its onboarding button + shared list-action labels:
    // short multi-word functional UI strings the model kept echoing with a different
    // capitalisation ("My Role"), which a case-sensitive check missed. Asserted here for
    // EVERY supported language so "does the coverage include all languages?" is a hard no.
    private static readonly string[] PersonaSurfaceUiKeys =
    {
        "SettingsMyPersonaTitle",   // "My role"
        "SettingsMyPersonaRole",    // "Role"
        "SettingsMyPersonaComplexity",
        "OnbGetStarted",            // "Get started"
        "SelectAll",                // "Select all"
        "DeleteSelected",           // "Delete selected"
    };

    public static IEnumerable<object[]> NonEnglishLanguages()
        => LocalizationManager.SupportedLanguages.Where(l => l != "en")
                                                 .Select(l => new object[] { l });

    [Theory]
    [MemberData(nameof(NonEnglishLanguages))]
    public void PersonaSurfaceUiLabels_AreLocalized_NotEnglishEcho(string lang)
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        var en = EmbeddedSection("en", "ui");
        try
        {
            lm.Language = lang;
            foreach (var key in PersonaSurfaceUiKeys)
            {
                string english = en[key].Trim().ToLowerInvariant();
                string actual = lm.GetUiString(key).Trim().ToLowerInvariant();
                Assert.True(actual != english,
                    $"{lang}.{key} still echoes the English source (case-insensitive): {lm.GetUiString(key)}");
            }
        }
        finally { lm.Language = original; }
    }

    [Theory]
    [MemberData(nameof(NonEnglishLanguages))]
    public void HotkeySettingsHeader_IsLocalized_NotEnglishEcho(string lang)
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        try
        {
            lm.Language = lang;
            // "HOTKEYS" echoed un-traduced in tr/ja/ko; every non-English language must
            // show its own term for this section header.
            Assert.NotEqual("hotkeys", lm.GetUiString("HotkeySettings").Trim().ToLowerInvariant());
        }
        finally { lm.Language = original; }
    }

    [Theory]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("zh")]
    public void CjkSoundAndPresetNames_UseNativeScript_NotLatin(string lang)
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        var enSounds = EmbeddedSection("en", "sounds");
        try
        {
            lm.Language = lang;
            // CJK UIs must render these in kana/hangul/hanzi (Latin-script languages keep
            // them as accepted international loanwords, which is why only ja/ko/zh are here).
            foreach (var id in new[] { "kick", "snare", "hihat", "clap", "tom", "rimshot",
                                       "crash", "gong", "sine", "square", "saw", "airhorn" })
                Assert.NotEqual(enSounds[id].ToLowerInvariant(),
                    lm.GetSoundDisplayName(id, enSounds[id]).ToLowerInvariant());
            Assert.NotEqual("android", lm.GetPresetDisplayName("Android").ToLowerInvariant());
        }
        finally { lm.Language = original; }
    }

    public static IEnumerable<object[]> LocalizedLanguages()
    {
        // languages whose shipped persona value for these keys differs from English
        foreach (var lang in new[] { "de", "fr", "es", "pt-BR", "ru", "it", "pl", "tr", "ja", "ko", "zh" })
            yield return new object[] { lang };
    }

    [Theory]
    [MemberData(nameof(LocalizedLanguages))]
    public void GetPersonaLabel_ReturnsShippedTargetLanguageValue(string lang)
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        try
        {
            lm.Language = lang;
            var expected = EmbeddedPersona(lang);
            // The runtime dictionary must match the shipped JSON, not silently fall
            // back to English -- that fallback is the "always shows English" symptom.
            Assert.Equal(expected[RoleKey], lm.GetPersonaLabel(RoleKey));
            Assert.Equal(expected[GroupKey], lm.GetPersonaLabel(GroupKey));
        }
        finally { lm.Language = original; }
    }

    [Fact]
    public void SwitchingLanguage_DoesNotLeavePersonaInEnglish()
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        try
        {
            lm.Language = "en";
            string en = lm.GetPersonaLabel(RoleKey);
            lm.Language = "de";
            string de = lm.GetPersonaLabel(RoleKey);
            Assert.NotEqual(en, de);        // a German user must see a different label
        }
        finally { lm.Language = original; }
    }

    [Fact]
    public void LanguageChanged_FiresAndPersonaIsAlreadyLocalizedWhenEventRaised()
    {
        var lm = LocalizationManager.Instance;
        string original = lm.Language;
        string? labelAtEventTime = null;
        bool sawLanguageEvent = false;
        void Handler(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationManager.Language))
            {
                sawLanguageEvent = true;
                labelAtEventTime = lm.GetPersonaLabel(RoleKey);   // read inside the event
            }
        }
        try
        {
            lm.PropertyChanged += Handler;
            lm.Language = "ja";
            lm.PropertyChanged -= Handler;

            Assert.True(sawLanguageEvent, "switching Language must raise PropertyChanged(\"Language\") so imperative listeners refresh");
            // The self-subscribed control rebuilds inside this event; it must already
            // see Japanese, never the pre-switch value -- otherwise the card strands.
            Assert.Equal(EmbeddedPersona("ja")[RoleKey], labelAtEventTime);
        }
        finally { lm.Language = original; }
    }
}
