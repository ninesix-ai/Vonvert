// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows;
using System.Windows.Controls;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Settings tab — audio device selection (input mic + output, VB-Cable friendly).
/// Tab is switched from the left sidebar rail (<see cref="Controls.SidebarNavRail"/>).
/// </summary>
public partial class MainWindow
{
    /// <summary>First time the Settings tab is shown (TabControl lazy-loads content).</summary>
    private void TabSettings_Loaded(object s, RoutedEventArgs e)
    {
        try { InitLanguageSelector(); } catch (Exception ex) { AppLog.Warning(ex, "InitLanguageSelector failed"); }
        try { InitThemeSelector(); } catch (Exception ex) { AppLog.Warning(ex, "InitThemeSelector failed"); }
        try { InitDataLocationView(); } catch (Exception ex) { AppLog.Warning(ex, "InitDataLocationView failed"); }
        try { InitFloatingSoundboardToggles(); } catch (Exception ex) { AppLog.Warning(ex, "InitFloatingSoundboardToggles failed"); }
        try { PopulateDevices(); } catch (Exception ex) { AppLog.Warning(ex, "TabSettings_Loaded: PopulateDevices failed"); }
        try { CheckVBCable(); } catch { /* VB-Cable check is non-critical */ }
    }

    // ── Language selector ──
    private bool _applyingLanguage;

    /// <summary>
    /// Fill the combo from <see cref="LocalizationManager.SupportedLanguageOptions"/> and
    /// reflect the current UI language without triggering a switch. The items come from the
    /// same list the manager validates against, so the picker can no longer drift from the
    /// shipped translations, and the selection is matched by language code because an
    /// index-based choice breaks the moment a language is added.
    /// </summary>
    private void InitLanguageSelector()
    {
        if (LanguageSelector == null) return;
        _applyingLanguage = true;
        try
        {
            var options = LocalizationManager.SupportedLanguageOptions;
            if (LanguageSelector.Items.Count != options.Length)
            {
                LanguageSelector.Items.Clear();
                foreach (var (code, nativeName) in options)
                    LanguageSelector.Items.Add(new ComboBoxItem { Tag = code, Content = nativeName });
            }

            string current = LocalizationManager.Instance.Language;
            int selected = 0;
            for (int i = 0; i < LanguageSelector.Items.Count; i++)
                if (LanguageSelector.Items[i] is ComboBoxItem item
                    && string.Equals((string?)item.Tag, current, StringComparison.Ordinal))
                    selected = i;
            LanguageSelector.SelectedIndex = selected;
        }
        finally { _applyingLanguage = false; }
    }

    /// <summary>Switch UI language; the manager persists the choice and refreshes all bindings.</summary>
    private void LanguageSelector_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_applyingLanguage) return;
        if (LanguageSelector?.SelectedItem is not ComboBoxItem item || item.Tag is not string code) return;
        LocalizationManager.Instance.Language = code;
    }

    // ── Theme selector ──
    private bool _applyingTheme;

    /// <summary>
    /// Fill the combo from the loaded palettes (name -> display name) and reflect the
    /// active theme without triggering a switch. Matched by theme name, and only rebuilt
    /// when the item count changes, so the picker cannot drift from ThemeEngine.
    /// </summary>
    private void InitThemeSelector()
    {
        if (ThemeSelector == null) return;
        _applyingTheme = true;
        try
        {
            var themes = ThemeEngine.Instance.Themes;
            if (ThemeSelector.Items.Count != themes.Count)
            {
                ThemeSelector.Items.Clear();
                foreach (var kv in themes)
                    ThemeSelector.Items.Add(new ComboBoxItem { Tag = kv.Key, Content = kv.Value.DisplayName });
            }

            string current = ThemeEngine.Instance.CurrentTheme?.Name ?? string.Empty;
            int selected = ThemeSelector.Items.Count > 0 ? 0 : -1;
            for (int i = 0; i < ThemeSelector.Items.Count; i++)
                if (ThemeSelector.Items[i] is ComboBoxItem item
                    && string.Equals((string?)item.Tag, current, StringComparison.Ordinal))
                    selected = i;
            ThemeSelector.SelectedIndex = selected;
        }
        finally { _applyingTheme = false; }
    }

    /// <summary>Switch the active palette; ThemeEngine persists the choice via AppConfig.</summary>
    private void ThemeSelector_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_applyingTheme) return;
        if (ThemeSelector?.SelectedItem is not ComboBoxItem item || item.Tag is not string name) return;
        ThemeEngine.Instance.ApplyTheme(name);
    }

    /// <summary>
    /// Hear Myself (ear-monitor) toggle: feed the processed voice back to the
    /// default output device (headphones) so the user can hear their own voice
    /// change while the main output goes to VB-Cable. Requires a graph restart
    /// (the monitor loopback is created when the audio graph is initialised).
    /// </summary>
    private void OnHearMyselfToggle(object s, RoutedEventArgs e)
    {
        if (!_uiReady || App.Engine == null) return;
        App.Engine.Settings.HearMyself = HearMyselfToggle?.IsChecked == true;
        if (App.Engine.State == EngineStatus.Active) _ = App.Engine.RestartAsync(App.Devices);
    }

    private void PopulateDevices()
    {
        if (MicSelector == null || OutSelector == null) return;
        if (App.Devices == null) return;

        // Detach events so auto-selecting defaults does not restart the engine.
        MicSelector.SelectionChanged  -= OnMicSelect;
        OutSelector.SelectionChanged -= OnOutSelect;

        try
        {
            var inputs  = App.Devices.InputDevices();
            var outputs = App.Devices.OutputDevices();

            MicSelector.ItemsSource       = inputs;
            MicSelector.DisplayMemberPath = "Name";
            if (inputs.Count > 0)
            {
                // Fall back to the SYSTEM DEFAULT mic when nothing saved matches —
                // never inputs[0], because a virtual cable (CABLE Output / VoiceMeeter)
                // often enumerates first and would silently replace the real mic.
                var selIn = DeviceSelection.ResolveInput(
                    inputs,
                    App.Engine.Settings.InputDeviceId,
                    App.Devices.DefaultInputDevice()?.ID);
                if (selIn != null)
                {
                    MicSelector.SelectedItem = selIn;
                    App.Engine.Settings.InputDeviceId = selIn.Id;
                }
            }
            else
            {
                MicSelector.ItemsSource = new List<AudioDevice> { new(string.Empty, L.NoDevices, 0, 0, 0) };
            }

            OutSelector.ItemsSource       = outputs;
            OutSelector.DisplayMemberPath = "Name";
            if (outputs.Count > 0)
            {
                var selOut = outputs.FirstOrDefault(d => d.Id == App.Engine.Settings.OutputDeviceId);
                if (selOut == null)
                {
                    // Prefer VB-Cable when present, otherwise the first output.
                    var vbIdx = outputs.ToList().FindIndex(
                        d => d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase));
                    OutSelector.SelectedIndex = vbIdx >= 0 ? vbIdx : 0;
                    if (OutSelector.SelectedItem is AudioDevice d
                        && App.Engine.Settings.OutputDeviceId != d.Id)
                        App.Engine.Settings.OutputDeviceId = d.Id;
                }
                else
                {
                    OutSelector.SelectedItem = selOut;
                }
            }
            else
            {
                OutSelector.ItemsSource = new List<AudioDevice> { new(string.Empty, L.NoDevices, 0, 0, 0) };
            }
        }
        finally
        {
            MicSelector.SelectionChanged  += OnMicSelect;
            OutSelector.SelectionChanged += OnOutSelect;
        }
    }

    private void OnMicSelect(object s, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (MicSelector.SelectedItem is AudioDevice d && !string.IsNullOrEmpty(d.Id)
            && App.Engine.Settings.InputDeviceId != d.Id)
        {
            App.Engine.Settings.InputDeviceId = d.Id;
            SaveDeviceSettings();
            if (App.Engine.State == EngineStatus.Active) _ = App.Engine.RestartAsync(App.Devices);
        }
    }

    private void OnOutSelect(object s, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (OutSelector.SelectedItem is AudioDevice d && !string.IsNullOrEmpty(d.Id)
            && App.Engine.Settings.OutputDeviceId != d.Id)
        {
            App.Engine.Settings.OutputDeviceId = d.Id;
            SaveDeviceSettings();
            if (App.Engine.State == EngineStatus.Active) _ = App.Engine.RestartAsync(App.Devices);
        }
    }

    private void OnRefreshDevices(object s, RoutedEventArgs e)
    {
        try
        {
            PopulateDevices();
            CheckVBCable();
        }
        catch (Exception ex) { AppLog.Warning(ex, "OnRefreshDevices failed"); }
    }

    private void SaveDeviceSettings()
    {
        try
        {
            AppConfig.Instance.Audio.InputDeviceId  = App.Engine.Settings.InputDeviceId;
            AppConfig.Instance.Audio.OutputDeviceId = App.Engine.Settings.OutputDeviceId;
            AppConfig.Instance.Save();
        }
        catch (Exception ex) { AppLog.Warning(ex, "SaveDeviceSettings failed"); }
    }
}
