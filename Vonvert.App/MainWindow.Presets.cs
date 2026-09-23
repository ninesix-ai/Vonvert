// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

public partial class MainWindow
{
    // ════ Preset Selection ════
    // Clicking a preset applies it to the DSP chain and updates the section title.
    // There is no parameter panel, no simple/pro mode and no search/filter UI —
    // the bundled female-voice presets are applied as-is.

    private string? _selectedPresetName;

    /// <summary>Editable working copy behind the expert panel, so tuning never
    /// mutates the shared built-in preset instance.</summary>
    private VoiceProfile? _editProfile;

    /// <summary>Set during the startup auto-apply so the panel seeds but stays hidden
    /// (the rail only slides open on a user-initiated voice pick, matching OSS).</summary>
    private bool _suppressParamExpand;

    /// <summary>Target width of the right parameter rail when expanded.</summary>
    private const double ParamRailWidth = 340;

    /// <summary>Event handler for MainPresetPicker.PresetChanged.</summary>
    private void MainPresetPicker_PresetChanged(VoiceProfile preset)
    {
        if (preset == null) return;
        var displayName = L.GetPresetDisplayName(preset.Name);

        // Toggle: clicking the already-selected voice while the rail is open closes it.
        if (_selectedPresetName == displayName
            && ParamRailBorder != null && ParamRailBorder.Visibility == Visibility.Visible)
        {
            CollapseParamPanel();
            return;
        }

        ApplyPreset(preset);
        _selectedPresetName = displayName;

        // Re-seed the expert panel with a fresh editable copy of the applied preset.
        _editProfile = preset.Clone();
        try { ExpertPanel?.Load(_editProfile); } catch (Exception ex) { AppLog.Warning(ex, "ExpertPanel.Load failed"); }

        // Update section title
        if (SectionTitle != null) SectionTitle.Text = displayName;

        // Novice feedback
        ShowToast(string.Format(L.ToastPresetApplied, displayName), "success");

        // Slide the rail open — except on the silent startup auto-apply.
        if (_suppressParamExpand) _suppressParamExpand = false;
        else ExpandParamPanel();
    }

    /// <summary>Slide the right parameter rail open (visible + width to <see cref="ParamRailWidth"/>).</summary>
    private void ExpandParamPanel()
    {
        if (ParamRailBorder == null) return;
        ParamRailBorder.Visibility = Visibility.Visible;
        var anim = new System.Windows.Media.Animation.DoubleAnimation(ParamRailWidth, TimeSpan.FromSeconds(0.2))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        ParamRailBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }

    /// <summary>Slide the right parameter rail closed (width to 0, then collapse).</summary>
    private void CollapseParamPanel()
    {
        if (ParamRailBorder == null) return;
        var anim = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.15))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        anim.Completed += (_, _) =>
        {
            if (ParamRailBorder != null) ParamRailBorder.Visibility = Visibility.Collapsed;
        };
        ParamRailBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }

    // ════ Expert parameter panel (right rail) ════

    /// <summary>Simple (essential groups) vs Professional (all groups) mode switch.</summary>
    private void ExpertMode_Changed(object s, SelectionChangedEventArgs e)
    {
        if (ExpertPanel == null || ExpertModeCombo == null) return;
        ExpertPanel.SetMode(ExpertModeCombo.SelectedIndex == 1);   // index 1 = Professional
    }

    /// <summary>Live-apply an expert-panel edit to the engine (raised by the panel).</summary>
    private void OnExpertParameterChanged()
    {
        if (_editProfile == null) return;
        ApplyPreset(_editProfile);
    }

    private void ExpertSaveAs_Click(object s, RoutedEventArgs e)
    {
        if (_editProfile == null || App.Presets == null || MainPresetPicker == null) return;
        var dlg = new Vonvert.App.Controls.SavePresetDialog(_editProfile.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.ChosenName)) return;

        var saved = _editProfile.Clone();
        saved.Name = dlg.ChosenName.Trim();
        App.Presets.Save(saved);

        _editProfile = saved;
        MainPresetPicker.SelectPresetByName(saved.Name);
        ShowToast(string.Format(L.ToastPresetSaved, saved.Name), "success");
    }

    private void ExpertReset_Click(object s, RoutedEventArgs e)
    {
        // Reload the working copy from the preset the picker has selected, discarding edits.
        var source = MainPresetPicker?.SelectedPreset;
        if (source == null) return;
        _editProfile = source.Clone();
        ExpertPanel?.Load(_editProfile);
        ApplyPreset(_editProfile);
    }

    /// <summary>
    /// Apply a preset to the live DSP chain. Only the engine parameters are
    /// touched — the parameter panel UI does not exist.
    /// </summary>
    private void ApplyPreset(VoiceProfile p)
    {
        try
        {
            _dspCoordinator!.ApplyPreset(p);
            _lastGain = p.PreAmpGain;
            App.Engine.SetGain(_lastGain);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "ApplyPreset failed: {Msg}", ex.Message);
        }
    }

    // ════ Preset Import / Export / Delete ════
    // The data layer (PresetManager) already does the work; these are the thin
    // UI glue: pick a file, call the manager, refresh the picker, surface a toast.

    private void OnImportPreset()
    {
        if (App.Presets == null || MainPresetPicker == null) return;
        var dlg = new OpenFileDialog
        {
            Title = L.ImportPreset,
            Filter = $"{L.PresetFileFilter}|*.vopreset|{L.AllFilesLabel}|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        VoiceProfile? imported;
        try { imported = App.Presets.Import(dlg.FileName); }
        catch (Exception ex) { AppLog.Warning(ex, "Import preset failed"); imported = null; }

        if (imported == null)
        {
            ShowToast(L.PresetImportFailed, "error");
            return;
        }
        // Refresh tiles and make the imported voice the active, applied selection.
        MainPresetPicker.SelectPresetByName(L.GetPresetDisplayName(imported.Name));
        ShowToast(string.Format(L.PresetImportedFmt, L.GetPresetDisplayName(imported.Name)), "success");
    }

    private void OnExportPreset(VoiceProfile? preset)
    {
        if (App.Presets == null || preset == null) return;   // nothing selected → no-op
        var dlg = new SaveFileDialog
        {
            Title = L.ExportPreset,
            Filter = $"{L.PresetFileFilter}|*.vopreset|{L.AllFilesLabel}|*.*",
            FileName = SanitizeFileName(preset.Name) + ".vopreset",
            DefaultExt = ".vopreset",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            App.Presets.Export(preset, dlg.FileName);
            ShowToast(string.Format(L.PresetExportedFmt, L.GetPresetDisplayName(preset.Name)), "success");
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "Export preset failed");
            ShowToast(L.PresetExportFailed, "error");
        }
    }

    private void OnDeletePreset(VoiceProfile preset)
    {
        if (App.Presets == null || MainPresetPicker == null) return;
        var name = L.GetPresetDisplayName(preset.Name);
        var confirm = MessageBox.Show(
            string.Format(L.DeletePresetConfirmFmt, name),
            L.DeletePreset, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        App.Presets.Delete(preset);
        MainPresetPicker.RefreshTiles();
        ShowToast(string.Format(L.PresetDeletedFmt, name), "success");
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    // ════ Metrics Timer ════

    private void StartMetricsTimer()
    {
        _metricsTimer.Tick += (_, _) => UpdateMetrics();
        _metricsTimer.Start();
    }

    private void UpdateMetrics()
    {
        try
        {
            if (App.Engine == null) return;
            var m = App.Engine.Stats;

            // Bottom bar VU bars (configurable track width via AppConfig)
            double bottomVuMax = AppConfig.Instance.Ui.VuMeterMaxWidth;
            if (BottomBar?.InputVU != null)  BottomBar.InputVU.Width  = Math.Clamp(m.InputLevel  * bottomVuMax, 0, bottomVuMax);
            if (BottomBar?.OutputVU != null) BottomBar.OutputVU.Width = Math.Clamp(m.OutputLevel * bottomVuMax, 0, bottomVuMax);

            if (BottomBar?.Latency == null || BottomBar.Status == null) return;

            if (App.Engine.State == EngineStatus.Active)
            {
                BottomBar.Latency.Text = $"{L.LatencyPrefix}{m.LatencyMs:0.0} ms";
                BottomBar.Status.Text  = m.Underruns > 0 ? string.Format(L.UnderrunsFmt, m.Underruns) : (_isEffectOn ? L.RunningEffects : L.StandbyPassthrough);
            }
            else
            {
                BottomBar.Latency.Text = L.LatencyDash;
                BottomBar.Status.Text  = App.Engine.State switch
                {
                    EngineStatus.Idle => L.EngineStateStopped,
                    EngineStatus.Active => L.EngineStateRunning,
                    EngineStatus.Faulted   => L.EngineStateError,
                    _ => App.Engine.State.ToString()
                };
            }

            // Render spectrum + pitch visualization
            RenderSpectrum();
            RenderPitch();
        }
        catch { /* never crash the UI thread from a timer */ }
    }
}
