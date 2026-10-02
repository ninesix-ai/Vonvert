// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Vonvert.App.UIServices;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.ProceduralAudio;
using Vonvert.Engine.Soundboard;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Soundboard tab: category-filtered grid of one-shot pads rendered from a bound view-model
/// collection. Clicking a pad plays it un-pitched through the engine mixer; the badge captures
/// a global hotkey; user-imported sounds can be deleted. Engine + SoundboardManager do the work.
/// </summary>
public partial class SoundboardViewControl : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private static SoundboardManager? Board => App.Soundboard;
    private IAppServices? AppServices => Window.GetWindow(this) as IAppServices;

    private const string UserFilter = "__user__";

    private readonly List<SoundPadItemViewModel> _all = new();
    private string _filter = "";          // "" = All, else an engine category name or UserFilter
    private string? _capturingId;         // soundId currently awaiting a key press
    private bool _rebuildingChips;        // re-entry guard for programmatic chip (re)selection
    private int _warmVersion;             // discards stale duration warm-ups on rebuild

    public SoundboardViewControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Header "floating board" toggle mirrors the main window's floating window state; the guard
    // prevents a programmatic IsChecked update from echoing back as a command.
    private MainWindow? _mainWinForFloat;
    private bool _syncingFloatToggle;

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        VolumeSlider.Value = Board?.Volume ?? 0.35f;
        VolumeSlider.ValueChanged += (_, args) => { if (Board != null) Board.Volume = (float)args.NewValue; };

        if (Board != null) Board.SoundsChanged += OnSoundsChanged;
        if (Board != null) Board.FavoritesChanged += OnFavoritesChanged;
        if (App.Engine != null) App.Engine.StatusChanged += OnEngineStatusChanged;
        L.PropertyChanged += OnLanguageChanged;

        if (Window.GetWindow(this) is MainWindow mw)
        {
            _mainWinForFloat = mw;
            mw.FloatingVisibilityChanged += OnFloatVisibilityChanged;
            _syncingFloatToggle = true;
            try { FloatToggle.IsChecked = mw.IsFloatingOpen; }
            finally { _syncingFloatToggle = false; }
        }

        RebuildPads();
        UpdateModeChips();
        UpdateStatusBar();
    }

    private void OnUnloaded(object s, RoutedEventArgs e)
    {
        if (Board != null) Board.SoundsChanged -= OnSoundsChanged;
        if (Board != null) Board.FavoritesChanged -= OnFavoritesChanged;
        if (App.Engine != null) App.Engine.StatusChanged -= OnEngineStatusChanged;
        L.PropertyChanged -= OnLanguageChanged;
        if (_mainWinForFloat != null) { _mainWinForFloat.FloatingVisibilityChanged -= OnFloatVisibilityChanged; _mainWinForFloat = null; }
    }

    private void OnSoundsChanged() => Dispatcher.BeginInvoke(new Action(RebuildPads));
    private void OnFavoritesChanged() => Dispatcher.BeginInvoke(new Action(RebuildPads));

    /// <summary>📌 button: toggles this pad's pin; FavoritesChanged then rebuilds the grid.</summary>
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        Board?.TogglePin(vm.Id);
        e.Handled = true;
    }

    /// <summary>Header toggle: commands the main window to show/hide the floating mini-player.</summary>
    private void OnFloatChecked(object s, RoutedEventArgs e)
    {
        if (_syncingFloatToggle) return;
        _mainWinForFloat?.SetFloatingVisible(true);
    }

    private void OnFloatUnchecked(object s, RoutedEventArgs e)
    {
        if (_syncingFloatToggle) return;
        _mainWinForFloat?.SetFloatingVisible(false);
    }

    private void OnFloatVisibilityChanged(bool open) => Dispatcher.BeginInvoke(new Action(() =>
    {
        _syncingFloatToggle = true;
        try { FloatToggle.IsChecked = open; }
        finally { _syncingFloatToggle = false; }
    }));
    private void OnEngineStatusChanged(EngineStatus status) => Dispatcher.Invoke(UpdateStatusBar);

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-localize chip labels, pad names and badges without disturbing the active filter.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            BuildChips();
            RelocalizePads();
            foreach (var vm in _all) vm.BadgeText = BadgeFor(vm);
            UpdateStatusBar();   // the bar's text is assigned, not XAML-bound, so refresh it here
        }));
    }

    private void UpdateStatusBar()
    {
        // The view holds no state rules: SoundboardStatusPolicy decides which of the three
        // mutually exclusive sentences applies and which hue carries it. Nothing here hides
        // the bar - it is permanent precisely so the answer is always on screen.
        SoundboardStatusPolicy.For(Board?.LiveMode ?? false,
                                   App.Engine?.State ?? EngineStatus.Idle,
                                   out var text);
        SbStatusBar.Tag = text.ChipSelector;
        // Localization keys live in the JSON string tables, not in WPF resource
        // dictionaries: FindResource would return the unresolved-resource sentinel
        // (MS.Internal.NamedObject) and blow up on the cast.
        SbStatusText.Text = L.GetUiString(text.TextKey);
    }

    // ════════ Catalogue ════════

    private void RebuildPads()
    {
        _all.Clear();
        if (Board != null)
        {
            foreach (var def in Board.Sounds)
            {
                _all.Add(new SoundPadItemViewModel(
                    def.Id, def.Name, def.Emoji, def.Category,
                    def.SourceType == SoundSourceType.File)
                {
                    HotkeyText = FormatHotkey(Board.GetHotkey(def.Id)),
                    IsPinned = Board.IsPinned(def.Id),
                });
            }
        }
        foreach (var vm in _all) vm.BadgeText = BadgeFor(vm);
        RelocalizePads();
        WarmDurations();
        BuildChips();
        ApplyFilter();
    }

    /// <summary>Apply the current language's built-in sound names to every pad.
    /// User-imported pads keep their file name (no localized label exists).</summary>
    private void RelocalizePads()
    {
        foreach (var vm in _all)
            vm.Name = L.GetSoundDisplayName(vm.Id, vm.EnglishName);
    }

    /// <summary>Resolves pad duration labels off the UI thread (first touch caches the
    /// PCM); stale runs are discarded via version guard when pads rebuild.</summary>
    private void WarmDurations()
    {
        int myVersion = ++_warmVersion;
        var targets = _all.ToList();
        var board = Board;
        if (board == null) return;
        Task.Run(() =>
        {
            foreach (var vm in targets)
            {
                string? text = board.GetSoundDurationSeconds(vm.Id) is double d
                    ? PadDurationFormat.Format(d) : null;
                if (myVersion != _warmVersion) return;   // pads rebuilt meanwhile → drop stale run
                Dispatcher.BeginInvoke(new Action(() => { vm.DurationText = text; }));
            }
        });
    }

    private void BuildChips()
    {
        _rebuildingChips = true;
        try
        {
            ChipPanel.Children.Clear();

            var entries = new List<(string key, string label)> { ("", L.CategoryAll) };
            if (Board != null)
                foreach (var cat in Board.Categories)
                    if (_all.Any(x => x.Category == cat))
                        entries.Add((cat, LocalizedCategory(cat)));
            if (_all.Any(x => x.IsUser))
                entries.Add((UserFilter, L.CatImported));

            foreach (var (key, label) in entries)
            {
                var rb = new RadioButton
                {
                    Style = (Style)FindResource("SbChip"),
                    Content = label,
                    GroupName = "SbFilter",
                    Tag = key,
                    IsChecked = key == _filter,
                };
                rb.Checked += OnChip_Checked;
                ChipPanel.Children.Add(rb);
            }

            // Guarantee a selection even if the previous filter's category disappeared.
            if (ChipPanel.Children.OfType<RadioButton>().All(r => r.IsChecked != true)
                && ChipPanel.Children.Count > 0)
                _filter = "";
        }
        finally { _rebuildingChips = false; }
    }

    private void OnChip_Checked(object sender, RoutedEventArgs e)
    {
        if (_rebuildingChips || sender is not RadioButton rb || rb.Tag is not string key) return;
        _filter = key;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<SoundPadItemViewModel> view = _filter switch
        {
            "" => _all,
            UserFilter => _all.Where(x => x.IsUser),
            _ => _all.Where(x => x.Category == _filter),
        };
        PadList.ItemsSource = view.ToList();
    }

    // ════════ Interactions ════════

    private void Pad_Up(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        if (Board == null || App.Engine == null) return;
        Board.Play(vm.Id, App.Engine);

        // Brief accent pulse on the pad to confirm playback started.
        vm.IsPlaying = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        timer.Tick += (_, _) => { timer.Stop(); vm.IsPlaying = false; };
        timer.Start();
    }

    // ════ Audition / Live mode ════

    private void OnModeAudition_Checked(object s, RoutedEventArgs e) => SetLiveMode(false);
    private void OnModeLive_Checked(object s, RoutedEventArgs e) => SetLiveMode(true);

    private void SetLiveMode(bool live)
    {
        if (Board == null) return;
        Board.LiveMode = live;
        AppConfig.Instance.Audio.SoundboardLiveMode = live;
        AppConfig.Instance.Save();
        // No transient toast: the permanent status bar under the chips already says who can
        // hear the pads, and it stays true after the popup would have faded.
        UpdateModeChips();
        UpdateStatusBar();
    }

    // The status bar's escape hatch for users who switched to Live by accident.
    private void SwitchToAudition_Click(object sender, RoutedEventArgs e) => SetLiveMode(false);

    private void UpdateModeChips()
    {
        bool live = Board?.LiveMode ?? false;
        ModeAuditionChip.IsChecked = !live;
        ModeLiveChip.IsChecked = live;
    }

    private void Pad_ClearHotkey(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        if (Board != null && Board.RemoveHotkey(vm.Id))
        {
            vm.HotkeyText = "";
            vm.BadgeText = BadgeFor(vm);
        }
        e.Handled = true;
    }

    private void Bind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        BeginCapture(vm);
    }

    private void BeginCapture(SoundPadItemViewModel vm)
    {
        CancelCapture();
        _capturingId = vm.Id;
        vm.BadgeText = L.SoundboardPressKey;
        CaptureHint.Visibility = Visibility.Visible;
        Keyboard.Focus(this);   // ensure OnPreviewKeyDown reaches us
    }

    private void CancelCapture()
    {
        if (_capturingId == null) return;
        var vm = _all.FirstOrDefault(x => x.Id == _capturingId);
        if (vm != null) vm.BadgeText = BadgeFor(vm);
        _capturingId = null;
        CaptureHint.Visibility = Visibility.Collapsed;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_capturingId == null) { base.OnPreviewKeyDown(e); return; }

        if (e.Key == Key.Escape) { CancelCapture(); e.Handled = true; return; }
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.System or Key.LWin or Key.RWin)
        { e.Handled = true; return; }   // wait for a real combo

        var mods = Keyboard.Modifiers;
        int vk = (int)KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);
        Board?.AssignHotkey(_capturingId, new SoundHotkeyBinding(
            vk,
            Ctrl:  (mods & ModifierKeys.Control) != 0,
            Alt:   (mods & ModifierKeys.Alt)     != 0,
            Shift: (mods & ModifierKeys.Shift)   != 0));

        var captured = _capturingId;
        CancelCapture();
        var vm = _all.FirstOrDefault(x => x.Id == captured);
        if (vm != null)
        {
            vm.HotkeyText = FormatHotkey(Board?.GetHotkey(captured));
            vm.BadgeText = BadgeFor(vm);
        }
        e.Handled = true;
    }

    // ════════ Import / delete ════════

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (Board == null) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Audio (*.wav;*.mp3;*.ogg;*.m4a)|*.wav;*.mp3;*.ogg;*.m4a|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        int failed = 0;
        foreach (var path in dlg.FileNames)
        {
            try { Board.ImportFile(path); }   // ImportFile raises SoundsChanged → RebuildPads
            catch { failed++; }
        }
        if (failed > 0)
            AppServices?.ShowNotification(L.SoundboardImportFailed, "error");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadItemViewModel vm) return;
        Board?.RemoveUserSound(vm.Id);   // raises SoundsChanged → RebuildPads
    }

    // ════════ Helpers ════════

    private string BadgeFor(SoundPadItemViewModel vm)
        => string.IsNullOrEmpty(vm.HotkeyText) ? "\u2328" : vm.HotkeyText;   // ⌨

    private static string FormatHotkey(SoundHotkeyBinding? b)
    {
        if (b == null) return "";
        var parts = new List<string>(4);
        if (b.Ctrl) parts.Add("Ctrl");
        if (b.Alt) parts.Add("Alt");
        if (b.Shift) parts.Add("Shift");
        string key;
        try { key = KeyInterop.KeyFromVirtualKey(b.VirtualKey).ToString(); }
        catch { key = "VK_" + b.VirtualKey; }
        parts.Add(key);
        return string.Join("+", parts);
    }

    private static string LocalizedCategory(string cat) => cat switch
    {
        "Drums"   => L.CatDrums,
        "Tones"   => L.CatTones,
        "SFX"     => L.CatSFX,
        "Memes"   => L.CatMemes,
        "Music"   => L.CatMusic,
        "Retro"   => L.CatRetro,
        "Ambient" => L.CatAmbient,
        "User"    => L.CatImported,
        _         => cat,
    };

    // The pad row view-model now lives in the shared Vonvert.App.UIServices.SoundPadItemViewModel,
    // so the docked tab and the floating mini-player bind one data shape and cannot drift.
}
