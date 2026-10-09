// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

/// <summary>
/// Background-music card: pick a built-in ambience clip or a user audio file, then
/// transport / volume / loop / duck the BGM that VoiceEngine mixes post-DSP into the
/// outgoing frame (un-pitched, ducked under the voice). Choices persist through
/// AppConfig; playback is never auto-resumed on launch — the user presses Play, and
/// the voice engine must be running for the music to be audible.
/// </summary>
public partial class MainWindow
{
    private bool _bgmUiReady;
    private bool _bgmSuppressSelChange;
    private DispatcherTimer? _bgmSaveTimer;

    // Audio-file filter for the custom-track picker. NAudio decodes these via
    // AudioFileReader; the description reuses the shared "all files" label.
    private const string BgmFileFilter = "*.wav;*.mp3;*.m4a;*.ogg;*.flac;*.wma;*.aac";

    /// <summary>Populate the clip list and restore persisted BGM settings. Called once
    /// from OnServicesInitialized after the engine and config are live.</summary>
    private void InitBgmCard()
    {
        if (BgmClipSelector == null) return;
        var bgm = App.Engine.BGM;

        // Build the ambience clip list with localized labels (Tag = the stable id).
        _bgmSuppressSelChange = true;
        BgmClipSelector.Items.Clear();
        foreach (var clip in BuiltinAmbienceLibrary.All)
            BgmClipSelector.Items.Add(new ComboBoxItem
            {
                Content = L.GetAmbienceDisplayName(clip.Id, clip.DisplayName),
                Tag = clip.Id,
            });

        // Restore the persisted mix settings.
        var audio = AppConfig.Instance.Audio;
        bgm.Volume = (float)Math.Clamp(audio.BgmVolume, 0.0, 1.0);
        bgm.IsLoop = audio.BgmLoop;
        if (BgmVolumeSlider != null) BgmVolumeSlider.Value = audio.BgmVolume;
        UpdateBgmVolumeText(audio.BgmVolume);
        if (BgmLoopToggle != null) BgmLoopToggle.IsChecked = audio.BgmLoop;
        if (BgmDuckToggle != null) BgmDuckToggle.IsChecked = audio.BgmDucking;
        App.Engine.Pipeline.Ducking.IsEnabled = audio.BgmDucking;

        // Load (but do not play) the persisted track so Play works immediately.
        var sound = BuiltinAmbienceLibrary.All.FirstOrDefault(s => s.Id == audio.BgmClipId);
        if (sound != null)
        {
            var path = BuiltinAmbienceLibrary.ResolveToCache(sound);
            if (path != null) { bgm.Load(path); SelectClipInCombo(audio.BgmClipId); }
            else audio.BgmClipId = string.Empty;
        }
        else if (!string.IsNullOrEmpty(audio.BgmCustomPath) && File.Exists(audio.BgmCustomPath))
        {
            bgm.Load(audio.BgmCustomPath);
            BgmClipSelector.SelectedIndex = -1;
        }

        UpdateBgmTrackText();
        _bgmSuppressSelChange = false;
        _bgmUiReady = true;
    }

    /// <summary>Re-translate the clip labels after a language switch (labels come from
    /// the ambience JSON section, so they must be refreshed imperatively).</summary>
    private void RefreshBgmClipLabels()
    {
        if (BgmClipSelector == null) return;
        _bgmSuppressSelChange = true;
        try
        {
            var clips = BuiltinAmbienceLibrary.All;
            for (int i = 0; i < BgmClipSelector.Items.Count && i < clips.Count; i++)
                if (BgmClipSelector.Items[i] is ComboBoxItem item)
                    item.Content = L.GetAmbienceDisplayName(clips[i].Id, clips[i].DisplayName);
        }
        finally { _bgmSuppressSelChange = false; }
    }

    private void OnBgmClipSelected(object s, SelectionChangedEventArgs e)
    {
        if (!_bgmUiReady || _bgmSuppressSelChange) return;
        if (BgmClipSelector.SelectedItem is not ComboBoxItem item || item.Tag is not string id) return;

        var sound = BuiltinAmbienceLibrary.All.FirstOrDefault(x => x.Id == id);
        if (sound == null) return;
        var path = BuiltinAmbienceLibrary.ResolveToCache(sound);
        if (path == null) { ShowToast(L.BgmNoTrack, "error"); return; }

        var bgm = App.Engine.BGM;
        bgm.Load(path);
        bgm.Play();
        AppConfig.Instance.Audio.BgmClipId = id;
        AppConfig.Instance.Audio.BgmCustomPath = string.Empty;
        UpdateBgmTrackText();
        ScheduleBgmSave();
    }

    private void OnBgmBrowse(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.BgmLoadFile,
            Filter = $"{L.AllFilesLabel}|{BgmFileFilter}",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var bgm = App.Engine.BGM;
            bgm.Load(dlg.FileName);
            bgm.Play();
            AppConfig.Instance.Audio.BgmClipId = string.Empty;
            AppConfig.Instance.Audio.BgmCustomPath = dlg.FileName;
            _bgmSuppressSelChange = true;
            BgmClipSelector.SelectedIndex = -1;
            _bgmSuppressSelChange = false;
            UpdateBgmTrackText();
            ScheduleBgmSave();
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "BGM custom file load failed");
            ShowToast(L.BgmNoTrack, "error");
        }
    }

    private void OnBgmPlay(object s, RoutedEventArgs e)
    {
        var bgm = App.Engine.BGM;
        if (bgm.IsReady) bgm.Play();
    }

    private void OnBgmPause(object s, RoutedEventArgs e) => App.Engine.BGM.Pause();

    private void OnBgmStop(object s, RoutedEventArgs e)
    {
        App.Engine.BGM.Stop();
        UpdateBgmTrackText();
    }

    private void OnBgmVolumeChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        App.Engine.BGM.Volume = (float)Math.Clamp(e.NewValue, 0.0, 1.0);
        UpdateBgmVolumeText(e.NewValue);
        if (_bgmUiReady) ScheduleBgmSave();
    }

    private void OnBgmLoopChanged(object s, RoutedEventArgs e)
    {
        if (!_bgmUiReady) return;
        bool loop = BgmLoopToggle?.IsChecked == true;
        App.Engine.BGM.IsLoop = loop;
        AppConfig.Instance.Audio.BgmLoop = loop;
        ScheduleBgmSave();
    }

    private void OnBgmDuckChanged(object s, RoutedEventArgs e)
    {
        if (!_bgmUiReady) return;
        bool duck = BgmDuckToggle?.IsChecked == true;
        App.Engine.Pipeline.Ducking.IsEnabled = duck;
        AppConfig.Instance.Audio.BgmDucking = duck;
        ScheduleBgmSave();
    }

    private void UpdateBgmVolumeText(double v)
    {
        if (BgmVolumeText != null) BgmVolumeText.Text = $"{(int)Math.Round(v * 100)}%";
    }

    private void UpdateBgmTrackText()
    {
        if (BgmTrackText == null) return;
        var bgm = App.Engine.BGM;
        BgmTrackText.Text = bgm.IsReady ? bgm.TrackName : L.BgmNoTrack;
    }

    private void SelectClipInCombo(string id)
    {
        if (BgmClipSelector == null) return;
        foreach (var obj in BgmClipSelector.Items)
            if (obj is ComboBoxItem it && it.Tag is string t && t == id)
            { BgmClipSelector.SelectedItem = it; return; }
        BgmClipSelector.SelectedIndex = -1;
    }

    /// <summary>Debounce config writes so a volume drag does not thrash the disk.</summary>
    private void ScheduleBgmSave()
    {
        _bgmSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _bgmSaveTimer.Stop();
        _bgmSaveTimer.Tick -= BgmSaveTick;
        _bgmSaveTimer.Tick += BgmSaveTick;
        _bgmSaveTimer.Start();
    }

    private void BgmSaveTick(object? s, EventArgs e)
    {
        _bgmSaveTimer?.Stop();
        AppConfig.Instance.Audio.BgmVolume = App.Engine.BGM.Volume;
        AppConfig.Instance.Save();
    }
}
