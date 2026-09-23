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

    // ── Static preset name dictionaries (for completeness verification) ──

    private static readonly Dictionary<string, string> EnPresetNames = LoadStaticPresetNames("en");
    private static readonly Dictionary<string, string> ZhPresetNames = LoadStaticPresetNames("zh");

    private static Dictionary<string, string> LoadStaticPresetNames(string lang)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"Vonvert.App.Translations.{lang}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return new();
        try
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var obj = JObject.Parse(json);
            return obj["presets"]?.ToObject<Dictionary<string, string>>() ?? new();
        }
        catch { return new(); }
    }

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
        }
        else
        {
            _strings = new();
            _presetNames = new();
            _paramStrings = new();
        }

        // English fallback: fill in any keys missing from the target language
        if (lang != "en" && TryLoadJson("en", out var enObj) && enObj != null)
        {
            var enStrings = enObj["ui"]?.ToObject<Dictionary<string, string>>();
            if (enStrings != null)
            {
                foreach (var kv in enStrings)
                    if (!_strings.ContainsKey(kv.Key))
                        _strings[kv.Key] = kv.Value;
            }
            var enParams = enObj["params"]?.ToObject<Dictionary<string, string>>();
            if (enParams != null)
            {
                foreach (var kv in enParams)
                    if (!_paramStrings.ContainsKey(kv.Key))
                        _paramStrings[kv.Key] = kv.Value;
            }
        }

        // Fire PropertyChanged for every public property so bindings refresh
        foreach (var prop in GetType().GetProperties()
                     .Where(p => p.PropertyType == typeof(string) && p.CanRead))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop.Name));
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
        // No valid saved preference — follow the OS UI language on first run
        // (zh-* → Chinese, otherwise English). The user can still override this
        // in Settings, which persists to config.json.
        LoadLanguage(DetectSystemLanguage());
    }

    /// <summary>
    /// Best-effort OS UI-culture detection: any "zh*" culture selects Chinese,
    /// everything else falls back to English. Only languages in
    /// <see cref="SupportedLanguages"/> can ever be returned.
    /// </summary>
    internal static string DetectSystemLanguage()
    {
        try
        {
            var ui = System.Globalization.CultureInfo.CurrentUICulture.Name;   // e.g. "zh-CN", "en-US"
            if (!string.IsNullOrEmpty(ui) && ui.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh";
        }
        catch (Exception ex) { AppLog.Warning(ex, "[LocalizationManager] DetectSystemLanguage failed"); }
        return "en";
    }
}
