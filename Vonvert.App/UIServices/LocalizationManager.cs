// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Vonvert.App.UIServices;

/// <summary>
/// Singleton localization manager providing multi-language UI strings.
/// The supported set is defined once in <see cref="SupportedLanguages"/>; anything
/// outside it falls back to English (LoadLanguage's English-fallback path).
/// Raises PropertyChanged for every key when Language changes so XAML bindings auto-refresh.
/// </summary>
public partial class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _language = "en";
    private Dictionary<string, string> _strings = new();

    /// <summary>All supported language codes.</summary>
    public static readonly string[] SupportedLanguages = { "en", "zh" };

    /// <summary>
    /// The Settings language picker, derived from <see cref="SupportedLanguages"/>:
    /// each code with the name speakers of that language read best (a native name is
    /// deliberately not translatable - "Deutsch" must stay "Deutsch" in every UI
    /// language). Adding a language means adding a translation file plus one row here;
    /// L10N-08 and L10N-10 fail if either half is forgotten.
    /// </summary>
    public static readonly (string Code, string NativeName)[] SupportedLanguageOptions =
    {
        ("en", "English"),
        ("zh", "简体中文"),
    };

    // ── Persisted config ────────────────────────────────────────────────

    private static string ConfigPath => Path.Combine(
        AppPaths.Root, "config.json");

    private LocalizationManager() { LoadLanguage("en"); }

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            if (!SupportedLanguages.Contains(value)) return;
            _language = value;
            LoadLanguage(value);
            SaveLanguagePreference();
        }
    }

    // String properties split into LocalizationManager.Strings.*.cs partial classes.

    /// <summary>
    /// Live label for the Push-to-Talk mode button ("Hold to talk" / "Hold to mute").
    /// Exposed as a bound string property on purpose: <see cref="LoadLanguage"/> raises
    /// PropertyChanged for every string property, so anything bound to this auto-refreshes
    /// when the UI language changes — no imperative re-assignment to remember. Toggle the
    /// mode and call <see cref="NotifyPttHoldModeChanged"/> to raise it on mode change too.
    /// </summary>
    public string PttHoldModeLabel =>
        Vonvert.App.App.Hotkeys?.PttHoldToMute == true ? PttHoldToMute : PttHoldToTalk;

    /// <summary>Raise a change for <see cref="PttHoldModeLabel"/> after the PTT mode is toggled.</summary>
    public void NotifyPttHoldModeChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PttHoldModeLabel)));
}
