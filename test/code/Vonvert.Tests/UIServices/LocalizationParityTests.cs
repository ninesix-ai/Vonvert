// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the localization contract for a data-driven language list:
//   * Every language in SupportedLanguages must expose exactly the same key sets as
//     en in all four sections (ui / presets / params / persona), so a new or renamed
//     string can never be added to one language only (a runtime English fallback
//     silently hiding an untranslated label is the bug class).
//   * Every supported code has an embedded resource and a native display name.
//   * System-culture resolution only ever yields a supported code, matching the
//     full name before the two-letter prefix.
//   * check_locale.py's language list stays derived from the same source of truth.
// Reads the same embedded resources the app loads at runtime.

namespace Vonvert.Tests.UIServices;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Vonvert.App;
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

    /// <summary>Every supported language code except English, which is the reference.</summary>
    public static IEnumerable<object[]> TranslatedLanguages =>
        LocalizationManager.SupportedLanguages
            .Where(c => c != "en")
            .Select(c => new object[] { c });

    private static void AssertSameKeys(string lang, string section)
    {
        var en = ReadTopLevel("en", section);
        var other = ReadTopLevel(lang, section);
        Assert.True(en.SetEquals(other),
            $"{section} key drift for '{lang}' — only-en: [{string.Join(",", en.Except(other))}], " +
            $"only-{lang}: [{string.Join(",", other.Except(en))}]");
        Assert.NotEmpty(en);
    }

    [Theory(DisplayName = "L10N-01: every supported language has the same ui key set as en")]
    [MemberData(nameof(TranslatedLanguages))]
    public void UiKeys_ParityAgainstEnglish(string lang) => AssertSameKeys(lang, "ui");

    [Theory(DisplayName = "L10N-02: every supported language has the same preset key set as en")]
    [MemberData(nameof(TranslatedLanguages))]
    public void PresetKeys_ParityAgainstEnglish(string lang) => AssertSameKeys(lang, "presets");

    [Theory(DisplayName = "L10N-05: every supported language has the same params key set as en")]
    [MemberData(nameof(TranslatedLanguages))]
    public void ParamKeys_ParityAgainstEnglish(string lang) => AssertSameKeys(lang, "params");

    [Theory(DisplayName = "L10N-11: every supported language has the same persona key set as en")]
    [MemberData(nameof(TranslatedLanguages))]
    public void PersonaKeys_ParityAgainstEnglish(string lang) => AssertSameKeys(lang, "persona");

    [Fact(DisplayName = "L10N-03: DetectSystemLanguage only ever returns a supported code")]
    public void DetectSystemLanguage_IsAlwaysSupported()
    {
        var lang = LocalizationManager.DetectSystemLanguage();
        Assert.Contains(lang, LocalizationManager.SupportedLanguages);
    }

    [Theory(DisplayName = "L10N-03b: culture resolution prefers the full name, then the prefix")]
    [InlineData("zh-CN", "zh")]      // existing behaviour must not regress
    [InlineData("zh-TW", "zh")]
    [InlineData("en-US", "en")]
    [InlineData("de-AT", "en")]      // "de" is not shipped yet -> must not be picked
    [InlineData("pt-BR", "en")]      // region-qualified code absent from SupportedLanguages
    [InlineData("xx-YY", "en")]      // unknown culture
    [InlineData("", "en")]           // empty culture name
    public void ResolveSystemLanguage_FollowsSupportedCodes(string culture, string expected)
        => Assert.Equal(expected, LocalizationManager.ResolveSystemLanguage(culture));

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

    [Fact(DisplayName = "L10N-07: the English fallback merges a missing section key instead of dropping it")]
    public void EnglishFallback_FillsMissingKeysInEverySection()
    {
        // The presets section had no fallback at all, so a language file that omits a
        // preset name surfaced the raw internal key. Test the extracted helper directly:
        // no fake resource file needed, and the rule is now identical for all sections.
        var enObj = JObject.Parse("""
            { "presets": { "Female": "Woman", "Robot": "Robo" },
              "ui":      { "Hello": "Hi" } }
            """);

        var presets = new Dictionary<string, string> { ["Female"] = "Frau" };
        LocalizationManager.ApplyEnglishFallback(presets, enObj, "presets");
        Assert.Equal("Frau", presets["Female"]);          // existing translation wins
        Assert.Equal("Robo", presets["Robot"]);           // missing key filled from English

        var ui = new Dictionary<string, string>();
        LocalizationManager.ApplyEnglishFallback(ui, enObj, "ui");
        Assert.Equal("Hi", ui["Hello"]);

        LocalizationManager.ApplyEnglishFallback(ui, null, "ui");   // null English object is a no-op
        Assert.Single(ui);
    }

    [Fact(DisplayName = "L10N-08: every supported language code has an embedded translation resource")]
    public void SupportedLanguages_HaveEmbeddedResources()
    {
        foreach (var code in LocalizationManager.SupportedLanguages)
            Assert.NotNull(AppAsm.GetManifestResourceStream($"Vonvert.App.Translations.{code}.json"));
    }

    [Fact(DisplayName = "L10N-10: the picker's native-name list covers exactly the supported codes, in order")]
    public void SupportedLanguageOptions_MatchSupportedCodes()
    {
        var codes = LocalizationManager.SupportedLanguageOptions.Select(o => o.Code).ToList();
        Assert.Equal(LocalizationManager.SupportedLanguages, codes);
        foreach (var (code, native) in LocalizationManager.SupportedLanguageOptions)
        {
            Assert.False(string.IsNullOrWhiteSpace(native), $"language '{code}' has no display name");
            Assert.DoesNotContain("?", native);   // guards against a mojibake'd literal
        }
    }

    [Fact(DisplayName = "L10N-09: check_locale.py iterates exactly the supported languages")]
    public void CheckLocaleTool_LanguageListMatchesSourceOfTruth()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor != null && !File.Exists(Path.Combine(cursor.FullName, "Vonvert.OSS.sln")))
            cursor = cursor.Parent;
        var script = Path.Combine(cursor?.FullName ?? "", "check_locale.py");
        Assert.True(File.Exists(script), $"check_locale.py not found from {AppContext.BaseDirectory}");

        // The script must derive its language list rather than restate it, otherwise
        // adding a translation file silently leaves that language unchecked in CI.
        var text = File.ReadAllText(script);
        Assert.Contains("SUPPORTED_LANGUAGES", text);
        Assert.DoesNotContain("for lang in (\"en\", \"zh\")", text);
    }
}
