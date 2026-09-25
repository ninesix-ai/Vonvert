// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Vonvert.App.UIServices;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.ProceduralAudio;
using Vonvert.Engine.Soundboard;

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
        UpdateEngineHint();
    }

    private void OnUnloaded(object s, RoutedEventArgs e)
    {
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
        EngineHint.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
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
        BuildChips();
        ApplyFilter();
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
        Board.Play(vm.Id, App.Engine);

        // Brief accent pulse on the pad to confirm playback started.
        vm.IsPlaying = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        timer.Tick += (_, _) => { timer.Stop(); vm.IsPlaying = false; };
        timer.Start();
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

        public SoundPadVM(string id, string name, string emoji, string category, bool isUser)
        {
            Id = id; Name = name; Emoji = emoji; Category = category; IsUser = isUser;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
