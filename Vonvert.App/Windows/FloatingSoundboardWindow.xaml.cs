// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vonvert.App.UIServices;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.ProceduralAudio;
using Vonvert.Engine.Services;
using Vonvert.Engine.Soundboard;

namespace Vonvert.App;

/// <summary>
/// Floating mini-player: an always-on-top, taskbar-hidden window showing ONLY the pads the
/// user pinned, so a streamer can fire a favorite sound without leaving the main window.
/// Playback reuses the shared SoundboardManager playback path (local audition always + optional engine
/// broadcast) and the shared pad styles/VM, so visuals cannot drift from the docked tab.
/// </summary>
public partial class FloatingSoundboardWindow : Window
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private static SoundboardManager? Board => App.Soundboard;

    private readonly ObservableCollection<SoundPadItemViewModel> _pads = new();

    public FloatingSoundboardWindow()
    {
        InitializeComponent();
        PadList.ItemsSource = _pads;

        Loaded += (_, _) =>
        {
            Topmost = AppConfig.Instance.Audio.FloatTopmost;
            RestorePosition();
            Rebuild();
            UpdateStatusBand();
            if (Board != null) { Board.FavoritesChanged += OnFavoritesChanged; Board.SoundsChanged += OnSoundsChanged; }
            if (App.Engine != null) App.Engine.StatusChanged += OnEngineStatusChanged;
            L.PropertyChanged += OnLanguageChanged;
        };
        Closed += (_, _) =>
        {
            if (Board != null) { Board.FavoritesChanged -= OnFavoritesChanged; Board.SoundsChanged -= OnSoundsChanged; }
            if (App.Engine != null) App.Engine.StatusChanged -= OnEngineStatusChanged;
            L.PropertyChanged -= OnLanguageChanged;
        };
        LocationChanged += (_, _) => SavePosition();
    }

    private void OnFavoritesChanged() => Dispatcher.BeginInvoke(new Action(Rebuild));
    private void OnSoundsChanged() => Dispatcher.BeginInvoke(new Action(Rebuild));
    private void OnEngineStatusChanged(EngineStatus _) => Dispatcher.BeginInvoke(new Action(UpdateStatusBand));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        => Dispatcher.BeginInvoke(new Action(() => { Rebuild(); UpdateStatusBand(); }));

    /// <summary>Rebuild the pinned pad list in pin order, dropping stale ids, then toggle the empty state.</summary>
    private void Rebuild()
    {
        var board = Board;
        _pads.Clear();
        if (board == null) { ApplyEmptyState(); return; }

        var sounds = board.Sounds;
        var byId = new Dictionary<string, SoundDefinition>();
        foreach (var s in sounds) byId[s.Id] = s;

        var ordered = FloatingSoundboardModel.OrderPinned(board.PinnedSoundIds, sounds.Select(x => x.Id).ToList());
        foreach (var id in ordered)
        {
            if (!byId.TryGetValue(id, out var def)) continue;
            var vm = new SoundPadItemViewModel(id, def.Name, def.Emoji, def.Category,
                        def.SourceType == SoundSourceType.File) { IsPinned = true };
            vm.Name = L.GetSoundDisplayName(id, def.Name);
            vm.DurationText = board.GetSoundDurationSeconds(id) is double d ? PadDurationFormat.Format(d) : null;
            _pads.Add(vm);
        }
        ApplyEmptyState();
    }

    private void ApplyEmptyState()
    {
        bool empty = _pads.Count == 0;
        EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        PadList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Pad_Up(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        var board = Board;
        if (board == null || App.Engine == null) return;
        board.Play(vm.Id, App.Engine);           // audition always; broadcast only in live mode
        vm.IsPlaying = true;                      // same 320ms pulse as the docked tab
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        t.Tick += (_, _) => { t.Stop(); vm.IsPlaying = false; };
        t.Start();
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        Board?.TogglePin(vm.Id);                  // FavoritesChanged → Rebuild removes it
        e.Handled = true;
    }

    private void UpdateStatusBand()
    {
        var kind = SoundboardStatusPolicy.For(Board?.LiveMode ?? false,
                                             App.Engine?.State ?? EngineStatus.Idle, out var text);
        StatusText.Text = L.GetUiString(text.TextKey);
        // Reuse the same hue tokens the docked status bar uses, keyed off the resolved state.
        StatusBand.Background = (Brush)Application.Current.Resources[
            SoundboardStatusPolicy.IsLocalOnly(kind) ? "OkWashBrush" : "WarnWashBrush"];
    }

    private void RestorePosition()
    {
        var cfg = AppConfig.Instance.Audio;
        if (cfg.FloatLeft is double l && cfg.FloatTop is double t) { Left = l; Top = t; }
        else { var wa = SystemParameters.WorkArea; Left = wa.Right - Width - 24; Top = wa.Bottom - Height - 24; }
        ClampToScreen();
    }

    private void SavePosition()
    {
        var cfg = AppConfig.Instance.Audio;
        cfg.FloatLeft = Left;
        cfg.FloatTop = Top;
        AppConfig.Instance.Save();
    }

    private void ClampToScreen()
    {
        var wa = SystemParameters.WorkArea;
        if (ActualWidth < 1 || ActualHeight < 1) return;
        Left = Math.Max(wa.Left, Math.Min(Left, wa.Right - ActualWidth));
        Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - ActualHeight));
    }
}
