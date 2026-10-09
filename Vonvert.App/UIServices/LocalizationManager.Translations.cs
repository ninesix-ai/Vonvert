// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Preset name cache (loaded lazily from JSON) ──────────────────

    private Dictionary<string, string> _presetNames = new();

    // Parameter-panel labels live in a separate "params" JSON section (not "ui")
    // so check_locale's ui-vs-C#-property parity check is unaffected; they are
    // looked up dynamically by the data-driven expert panel via GetParamLabel.
    private Dictionary<string, string> _paramStrings = new();

    // Role/persona labels (role names, descriptions, group headings, complexity tiers)
    // live in a separate "persona" JSON section (not "ui") for the same reason: they are
    // looked up dynamically by persona key via GetPersonaLabel, so they don't need C# properties.
    private Dictionary<string, string> _personaStrings = new();

    // Built-in soundboard pad names live in a "sounds" JSON section keyed by the
    // stable sound Id (kick/airhorn/...), NOT by the English name: the Id never
    // changes across languages, so it is the reliable lookup key. Looked up
    // dynamically by GetSoundDisplayName, so no C# properties are needed.
    private Dictionary<string, string> _soundNames = new();

    // Built-in BGM ambience clip names live in an "ambience" JSON section keyed by
    // the stable clip Id (rain/white-noise/...), mirroring how "sounds" pads are
    // localized: the Id is language-invariant, so it is looked up dynamically by
    // GetAmbienceDisplayName and needs no C# string property.
    private Dictionary<string, string> _ambienceNames = new();

    // ── Generic getter (avoids ambiguity with G() overloads) ─────────

    private string G([CallerMemberName] string? key = null)
        => _strings.TryGetValue(key!, out var v) ? v : key!;

    /// <summary>
    /// Returns the localized string for a key (for data-driven UI).
    /// When the key is missing it returns the key itself (same behavior as <see cref="G"/>);
    /// LoadLanguage already merges the English fallback into the dictionary, so non-English
    /// locales automatically fall back to English for missing keys.
    /// </summary>
    public string GetUiString(string key)
        => !string.IsNullOrEmpty(key) && _strings.TryGetValue(key, out var v) ? v : key ?? "";

    /// <summary>
    /// Label for a data-driven parameter-panel key (from the JSON "params" section).
    /// Falls back to English (merged at load) then to the raw key.
    /// </summary>
    public string GetParamLabel(string key)
        => !string.IsNullOrEmpty(key) && _paramStrings.TryGetValue(key, out var v) ? v : key ?? "";

    /// <summary>
    /// Label for a data-driven persona key (from the JSON "persona" section): role names,
    /// descriptions, group headings and complexity tiers. Falls back to English (merged at
    /// load) then to the raw key.
    /// </summary>
    public string GetPersonaLabel(string key)
        => !string.IsNullOrEmpty(key) && _personaStrings.TryGetValue(key, out var v) ? v : key ?? "";

    // ── Load translations from JSON ──────────────────────────────────

    /// <summary>
    /// Load UI strings and preset names for the given language from
    /// the embedded JSON resource. Falls back to English if the target
    /// language file is missing or incomplete.
    /// </summary>
    private void LoadLanguage(string lang)
    {
        _language = lang;

        if (!TryLoadJson(lang, out var obj))
        {
            if (lang != "en")
                TryLoadJson("en", out obj);
        }

        if (obj != null)
        {
            _strings = obj["ui"]?.ToObject<Dictionary<string, string>>() ?? new();
            _presetNames = obj["presets"]?.ToObject<Dictionary<string, string>>() ?? new();
            _paramStrings = obj["params"]?.ToObject<Dictionary<string, string>>() ?? new();
            _personaStrings = obj["persona"]?.ToObject<Dictionary<string, string>>() ?? new();
            _soundNames = obj["sounds"]?.ToObject<Dictionary<string, string>>() ?? new();
            _ambienceNames = obj["ambience"]?.ToObject<Dictionary<string, string>>() ?? new();
        }
        else
        {
            _strings = new();
            _presetNames = new();
            _paramStrings = new();
            _personaStrings = new();
            _soundNames = new();
            _ambienceNames = new();
        }

        // English fallback: fill in any keys the target language is missing, in all
        // four sections. "presets" used to be skipped here, so a language file that
        // omitted a preset name surfaced the raw internal key instead of the label.
        if (lang != "en" && TryLoadJson("en", out var enObj))
        {
            ApplyEnglishFallback(_strings, enObj, "ui");
            ApplyEnglishFallback(_presetNames, enObj, "presets");
            ApplyEnglishFallback(_paramStrings, enObj, "params");
            ApplyEnglishFallback(_personaStrings, enObj, "persona");
            ApplyEnglishFallback(_soundNames, enObj, "sounds");
            ApplyEnglishFallback(_ambienceNames, enObj, "ambience");
        }

        // Fire PropertyChanged for every public property so bindings refresh
        foreach (var prop in GetType().GetProperties()
                     .Where(p => p.PropertyType == typeof(string) && p.CanRead))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop.Name));
    }

    /// <summary>
    /// Copy every key of <paramref name="enObj"/>'s <paramref name="section"/> into
    /// <paramref name="target"/> that the target language does not already define.
    /// Translations always win; English only fills gaps, so an untranslated string
    /// degrades to readable English rather than to a raw key name.
    /// </summary>
    internal static void ApplyEnglishFallback(
        Dictionary<string, string> target, JObject? enObj, string section)
    {
        var en = enObj?[section]?.ToObject<Dictionary<string, string>>();
        if (en == null) return;
        foreach (var kv in en)
            if (!target.ContainsKey(kv.Key))
                target[kv.Key] = kv.Value;
    }

    /// <summary>
    /// Try to load a translation JSON file for the given language code.
    /// Returns true if successful, with the parsed JObject in <paramref name="obj"/>.
    /// </summary>
    private static bool TryLoadJson(string lang, out JObject? obj)
    {
        obj = null;

        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"Vonvert.App.Translations.{lang}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            try
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                obj = JObject.Parse(json);
                return true;
            }
            catch { /* fall through */ }
        }

        return false;
    }

    // ── Preset display name lookup ───────────────────────────────────

    /// <summary>Returns the localized display name for a built-in preset,
    /// or the original name for custom presets.</summary>
    public string GetPresetDisplayName(string englishName)
    {
        return _presetNames.TryGetValue(englishName, out var localized)
            ? localized
            : englishName;
    }

    /// <summary>
    /// Localized display name for a built-in soundboard pad, looked up by its stable
    /// Id (kick/airhorn/...). User-imported sounds and any unknown Id fall back to
    /// <paramref name="englishFallback"/> (the sound's built-in name or its file name),
    /// so a pad never shows a raw id. English fallback in LoadLanguage already fills
    /// missing "sounds" keys, so non-English locales degrade to English, not to the id.
    /// </summary>
    public string GetSoundDisplayName(string id, string englishFallback)
        => !string.IsNullOrEmpty(id) && _soundNames.TryGetValue(id, out var localized)
            ? localized
            : englishFallback;

    /// <summary>
    /// Localized display name for a built-in BGM ambience clip, looked up by its
    /// stable Id (rain/white-noise/...). Any unknown Id falls back to
    /// <paramref name="englishFallback"/>, and English fallback in LoadLanguage fills
    /// missing "ambience" keys, so a non-English locale degrades to English, not to
    /// the raw id.
    /// </summary>
    public string GetAmbienceDisplayName(string id, string englishFallback)
        => !string.IsNullOrEmpty(id) && _ambienceNames.TryGetValue(id, out var localized)
            ? localized
            : englishFallback;

    // ── Persistence ──────────────────────────────────────────────────

    private void SaveLanguagePreference()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonConvert.SerializeObject(new { language = _language }));
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[LocalizationManager] SaveLanguagePreference failed");
        }
    }

    public void LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var obj = JsonConvert.DeserializeObject<JObject>(json);
            string? lang = (string?)obj?["language"];
            if (!string.IsNullOrEmpty(lang) && SupportedLanguages.Contains(lang))
            {
                LoadLanguage(lang);
                _language = lang;
                return;
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "[LocalizationManager] LoadSavedLanguage failed"); }
        // No valid saved preference — follow the OS UI language on first run,
        // resolving it against SupportedLanguages. The user can still override this
        // in Settings, which persists to config.json.
        LoadLanguage(DetectSystemLanguage());
    }

    /// <summary>
    /// Best-effort OS UI-culture detection; only ever yields a code from
    /// <see cref="SupportedLanguages"/>.
    /// </summary>
    internal static string DetectSystemLanguage()
    {
        try
        {
            return ResolveSystemLanguage(System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
        catch (Exception ex) { AppLog.Warning(ex, "[LocalizationManager] DetectSystemLanguage failed"); }
        return "en";
    }

    /// <summary>
    /// Map an OS culture name (e.g. "zh-CN", "pt-BR") onto a supported language code:
    /// the full name must match first so a region-qualified code is honoured, then the
    /// primary sub-tag, and anything unsupported degrades to English. Kept free of
    /// environment access so every branch is unit-testable (L10N-03b).
    /// </summary>
    internal static string ResolveSystemLanguage(string? uiCultureName)
    {
        if (string.IsNullOrWhiteSpace(uiCultureName)) return "en";

        // 1) Exact culture match, so "pt-BR" resolves to pt-BR rather than to a bare "pt".
        foreach (var code in SupportedLanguages)
            if (string.Equals(uiCultureName, code, StringComparison.OrdinalIgnoreCase))
                return code;

        // 2) Primary sub-tag match: "de-AT" -> "de", "zh-TW" -> "zh". Where several
        //    region variants of one language exist, the first listed code wins.
        var primary = uiCultureName.Split('-')[0];
        foreach (var code in SupportedLanguages)
            if (string.Equals(code.Split('-')[0], primary, StringComparison.OrdinalIgnoreCase))
                return code;

        // 3) Not on the supported list.
        return "en";
    }
}
