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

    private readonly List<SoundPadVM> _all = new();
    private string _filter = "";          // "" = All, else an engine category name or UserFilter
    private string? _capturingId;         // soundId currently awaiting a key press
    private bool _rebuildingChips;        // re-entry guard for programmatic chip (re)selection

    // Audition progress tracking: pad soundId → playback token from the mixer.
    private readonly Dictionary<string, long> _padTokens = new();
    private DispatcherTimer? _progressTimer;
    private int _warmVersion;             // discards stale duration warm-ups on rebuild

    public SoundboardViewControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        VolumeSlider.Value = Board?.Volume ?? 0.35f;
        VolumeSlider.ValueChanged += (_, args) => { if (Board != null) Board.Volume = (float)args.NewValue; };

        if (Board != null) Board.SoundsChanged += OnSoundsChanged;
        if (App.Engine != null) App.Engine.StatusChanged += OnEngineStatusChanged;
        L.PropertyChanged += OnLanguageChanged;

        RebuildPads();
        UpdateModeChips();
        UpdateEngineHint();
    }

    private void OnUnloaded(object s, RoutedEventArgs e)
    {
        _padTokens.Clear();
        _progressTimer?.Stop();
        if (Board != null) Board.SoundsChanged -= OnSoundsChanged;
        if (App.Engine != null) App.Engine.StatusChanged -= OnEngineStatusChanged;
        L.PropertyChanged -= OnLanguageChanged;
    }

    private void OnSoundsChanged() => Dispatcher.BeginInvoke(new Action(RebuildPads));
    private void OnEngineStatusChanged(EngineStatus status) => Dispatcher.Invoke(UpdateEngineHint);

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-localize chip labels and badges without disturbing the active filter.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            BuildChips();
            foreach (var vm in _all) vm.BadgeText = BadgeFor(vm);
        }));
    }

    private void UpdateEngineHint()
    {
        bool running = App.Engine != null && App.Engine.State == EngineStatus.Active;
        bool live = Board?.LiveMode ?? false;
        // Audition mode plays locally regardless of engine state, so the "start the
        // engine" hint (bound to SoundboardEngineHint) is only relevant in live mode.
        EngineHint.Visibility = (live && !running) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ════════ Catalogue ════════

    private void RebuildPads()
    {
        _all.Clear();
        if (Board != null)
        {
            foreach (var def in Board.Sounds)
            {
                _all.Add(new SoundPadVM(
                    def.Id, def.Name, def.Emoji, def.Category,
                    def.SourceType == SoundSourceType.File)
                {
                    HotkeyText = FormatHotkey(Board.GetHotkey(def.Id)),
                });
            }
        }
        foreach (var vm in _all) vm.BadgeText = BadgeFor(vm);
        WarmDurations();
        BuildChips();
        ApplyFilter();
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
                string text = board.GetSoundDurationSeconds(vm.Id) is double d
                    ? PadDurationFormat.Format(d) : "";
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
        IEnumerable<SoundPadVM> view = _filter switch
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
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadVM vm) return;
        if (Board == null || App.Engine == null) return;
        long token = Board.Play(vm.Id, App.Engine);
        RegisterPadPlayback(vm.Id, token);
    }

    /// <summary>Binds a fresh playback token to its pad and starts the 100 ms poll
    /// lazily. Token 0 (no local audition) keeps the legacy 320 ms accent pulse.</summary>
    private void RegisterPadPlayback(string soundId, long token)
    {
        var vm = _all.FirstOrDefault(v => v.Id == soundId);
        if (vm == null) return;
        vm.IsPlaying = true;
        if (token != 0)
        {
            _padTokens[soundId] = token;
            _progressTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _progressTimer.Tick -= OnProgressTick;
            _progressTimer.Tick += OnProgressTick;
            _progressTimer.Start();
        }
        else
        {
            var pulse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            pulse.Tick += (_, _) => { pulse.Stop(); vm.IsPlaying = false; };
            pulse.Start();
        }
    }

    private void OnProgressTick(object? s, EventArgs e)
    {
        var audition = App.Audition;
        foreach (var (id, token) in _padTokens.ToList())
        {
            var vm = _all.FirstOrDefault(v => v.Id == id);
            if (vm == null) { _padTokens.Remove(id); continue; }
            if (audition != null && audition.TryGetProgress(token, out int pos, out int len) && len > 0)
            {
                vm.Progress = (double)pos / len;
            }
            else
            {
                _padTokens.Remove(id);
                vm.Progress = 0;
                vm.IsPlaying = false;
            }
        }
        if (_padTokens.Count == 0) _progressTimer?.Stop();
    }

    /// <summary>External trigger entry (global hotkeys fire on MainWindow): gives the
    /// pad the same pulse + progress bar as a mouse click.</summary>
    public void NotifyPlayed(string soundId, long token)
        => Dispatcher.InvokeAsync(() => RegisterPadPlayback(soundId, token));

    // ════ Audition / Live mode ════

    private void OnModeAudition_Checked(object s, RoutedEventArgs e) => SetLiveMode(false);
    private void OnModeLive_Checked(object s, RoutedEventArgs e) => SetLiveMode(true);

    private void SetLiveMode(bool live)
    {
        if (Board == null) return;
        Board.LiveMode = live;
        AppConfig.Instance.Audio.SoundboardLiveMode = live;
        AppConfig.Instance.Save();
        if (live) AppServices?.ShowNotification(L.SoundboardLiveModeNotice, "warning");
        UpdateModeChips();
        UpdateEngineHint();
    }

    private void UpdateModeChips()
    {
        bool live = Board?.LiveMode ?? false;
        ModeAuditionChip.IsChecked = !live;
        ModeLiveChip.IsChecked = live;
    }

    private void Pad_ClearHotkey(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadVM vm) return;
        if (Board != null && Board.RemoveHotkey(vm.Id))
        {
            vm.HotkeyText = "";
            vm.BadgeText = BadgeFor(vm);
        }
        e.Handled = true;
    }

    private void Bind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadVM vm) return;
        BeginCapture(vm);
    }

    private void BeginCapture(SoundPadVM vm)
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
        if (sender is not FrameworkElement fe || fe.DataContext is not SoundPadVM vm) return;
        Board?.RemoveUserSound(vm.Id);   // raises SoundsChanged → RebuildPads
    }

    // ════════ Helpers ════════

    private string BadgeFor(SoundPadVM vm)
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

    /// <summary>Row view-model for a single soundboard pad. Only the mutable bits notify.</summary>
    private sealed class SoundPadVM : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Name { get; }
        public string Emoji { get; }
        public string Category { get; }
        public bool IsUser { get; }
        public string HotkeyText { get; set; } = "";

        private string _badge = "";
        public string BadgeText
        {
            get => _badge;
            set { if (_badge != value) { _badge = value; OnChanged(nameof(BadgeText)); } }
        }

        private bool _playing;
        public bool IsPlaying
        {
            get => _playing;
            set { if (_playing != value) { _playing = value; OnChanged(nameof(IsPlaying)); } }
        }

        private double _progress;
        /// <summary>0..1 audition playback progress for this pad (0 = idle/hidden).</summary>
        public double Progress
        {
            get => _progress;
            set { if (Math.Abs(_progress - value) > 0.001) { _progress = value; OnChanged(nameof(Progress)); } }
        }

        private string _durationText = "";
        /// <summary>Static m:ss label shown at the pad corner; empty while unresolved.</summary>
        public string DurationText
        {
            get => _durationText;
            set { if (_durationText != value) { _durationText = value; OnChanged(nameof(DurationText)); } }
        }

        public SoundPadVM(string id, string name, string emoji, string category, bool isUser)
        {
            Id = id; Name = name; Emoji = emoji; Category = category; IsUser = isUser;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
