// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Vonvert.App.Controls;
using Vonvert.App.UIServices;

namespace Vonvert.App;

/// <summary>
/// Global-hotkey wiring for the main window: engine power / mute / next-preset
/// actions, Push-to-Talk (Hold-to-Talk and Hold-to-Mute), and the Settings
/// re-binding capture UI. All Win32 registration lives in <see cref="HotkeyService"/>;
/// this partial only bridges service events to window state and the DSP gain.
/// </summary>
public partial class MainWindow
{
    // ── State ──
    private bool _isMuted = false;
    private PttPolicy.State _ptt = PttPolicy.Idle(false);
    private HotkeyService.HotkeyAction? _capturingHotkeyAction;
    /// <summary>Guards against re-entrant Checked events while the mode radios are synced programmatically.</summary>
    private bool _suppressPttRadioEvents;

    /// <summary>
    /// Subscribe to the hotkey service, attach it to this window's HWND and
    /// enable re-binding capture. Called once from OnServicesInitialized, after
    /// the window source (HWND) exists.
    /// </summary>
    private void BindHotkeys()
    {
        var hk = App.Hotkeys;
        if (hk == null) return;

        hk.HotkeyPressed += OnHotkeyPressed;
        hk.PushToTalkStateChanged += OnPushToTalkStateChanged;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            hk.Register(hwnd);

        // Soundboard global hotkeys: register persisted bindings, re-register on
        // change, and play the mapped sound when one fires.
        if (App.Soundboard != null)
        {
            hk.SoundHotkeyFired += OnSoundHotkeyFired;
            App.Soundboard.HotkeysChanged += ReregisterSoundHotkeys;
            ReregisterSoundHotkeys();
        }

        UpdateHotkeyButtonLabels();
        KeyDown += OnWindowKeyDown;
    }

    private void ReregisterSoundHotkeys()
    {
        var hk = App.Hotkeys;
        var sb = App.Soundboard;
        if (hk == null || sb == null) return;
        hk.RegisterSoundHotkeys(sb.HotkeyBindings);
    }

    private void OnSoundHotkeyFired(string soundId)
    {
        // Fires on the UI thread (WndProc); InvokeAsync is safe during shutdown.
        Dispatcher.InvokeAsync(() =>
        {
            if (!_servicesInitialized) return;
            try { App.Soundboard?.Play(soundId, App.Engine); }
            catch (Exception ex) { AppLog.Warning(ex, "Soundboard hotkey play failed: {Id}", soundId); }
        });
    }

    // ── Hotkey dispatch ──
    private void OnHotkeyPressed(HotkeyService.HotkeyAction action)
    {
        // Fires on the UI thread (WndProc), but InvokeAsync is safe during shutdown.
        Dispatcher.InvokeAsync(() =>
        {
            if (!_servicesInitialized) return;
            switch (action)
            {
                case HotkeyService.HotkeyAction.TogglePower: TogglePower(); break;
                case HotkeyService.HotkeyAction.ToggleMute:  ToggleMute();  break;
                case HotkeyService.HotkeyAction.CyclePreset: CyclePreset(); break;
            }
        });
    }

    private void OnPushToTalkStateChanged(bool isPressed)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_servicesInitialized) ApplyPushToTalk(isPressed);
        });
    }

    // ── Actions ──
    private void ToggleMute()
    {
        _isMuted = !_isMuted;
        if (!_ptt.Active) _ptt = PttPolicy.Idle(_isMuted);
        App.Engine.SetGain(_isMuted ? 0f : _lastGain);
    }

    private void CyclePreset()
    {
        try { MainPresetPicker?.CycleToNext(); }
        catch (Exception ex) { AppLog.Warning(ex, "CyclePreset failed"); }
    }

    /// <summary>
    /// Push-to-Talk transition. The pure state machine decides the new mute flag;
    /// the window applies gain and, for Hold-to-Talk, ensures the engine is running.
    /// </summary>
    private void ApplyPushToTalk(bool isPressed)
    {
        bool holdToMute = App.Hotkeys?.PttHoldToMute ?? false;
        _ptt = PttPolicy.OnChange(_ptt, isPressed, holdToMute);
        _isMuted = _ptt.Muted;
        App.Engine.SetGain(_isMuted ? 0f : _lastGain);

        // Hold-to-Talk: opening the gate should also bring the engine up if the
        // user had powered it off entirely.
        if (isPressed && !holdToMute && !_isEffectOn)
            TogglePower();
    }

    // ── Label sync ──
    private void UpdateHotkeyButtonLabels()
    {
        var hk = App.Hotkeys;
        if (hk == null) return;
        if (hk.Bindings.TryGetValue(HotkeyService.HotkeyAction.TogglePower, out var b1) && HotkeyToggleTxt != null)
            HotkeyToggleTxt.Text = HotkeyService.FormatKey(b1);
        if (hk.Bindings.TryGetValue(HotkeyService.HotkeyAction.ToggleMute, out var b2) && HotkeyMuteTxt != null)
            HotkeyMuteTxt.Text = HotkeyService.FormatKey(b2);
        if (hk.Bindings.TryGetValue(HotkeyService.HotkeyAction.CyclePreset, out var b3) && HotkeyPresetTxt != null)
            HotkeyPresetTxt.Text = HotkeyService.FormatKey(b3);
        if (hk.Bindings.TryGetValue(HotkeyService.HotkeyAction.PushToTalk, out var b4) && HotkeyPttTxt != null)
            HotkeyPttTxt.Text = HotkeyService.FormatKey(b4);

        UpdatePttModeSection();
    }

    private void UpdatePttModeSection()
    {
        var hk = App.Hotkeys;
        if (hk == null) return;
        if (PttHoldToTalkRadio == null || PttHoldToMuteRadio == null) return;

        // Sync selection without re-triggering the user handler. Radio labels are
        // XAML-bound and re-localize on their own; only the caption below, which
        // embeds the live key name, is set imperatively.
        _suppressPttRadioEvents = true;
        try
        {
            if (hk.PttHoldToMute) PttHoldToMuteRadio.IsChecked = true;
            else PttHoldToTalkRadio.IsChecked = true;
        }
        finally { _suppressPttRadioEvents = false; }

        if (PttWhenHeldTxt != null)
        {
            string keyName = hk.Bindings.TryGetValue(HotkeyService.HotkeyAction.PushToTalk, out var pb)
                ? HotkeyService.FormatKey(pb) : "—";
            PttWhenHeldTxt.Text = string.Format(L.PttWhenHeldLabel, keyName);
        }
    }

    // ── Re-binding capture ──
    private void HotkeyBtn_Click(object s, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        if (s is not Button btn || btn.Tag is not string actionStr) return;
        if (!Enum.TryParse<HotkeyService.HotkeyAction>(actionStr, out var action)) return;

        _capturingHotkeyAction = action;
        HideConflictWarning();
        SelectableTextBlock? target = action switch
        {
            HotkeyService.HotkeyAction.TogglePower => HotkeyToggleTxt,
            HotkeyService.HotkeyAction.ToggleMute  => HotkeyMuteTxt,
            HotkeyService.HotkeyAction.CyclePreset => HotkeyPresetTxt,
            HotkeyService.HotkeyAction.PushToTalk  => HotkeyPttTxt,
            _ => null
        };
        if (target != null) target.Text = L.HotkeyPressKey;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingHotkeyAction == null) return;
        var hk = App.Hotkeys;
        if (hk == null) { _capturingHotkeyAction = null; return; }

        var wpfKey = e.Key;
        if (wpfKey == Key.Escape)
        {
            _capturingHotkeyAction = null;
            HideConflictWarning();
            UpdateHotkeyButtonLabels();
            e.Handled = true;
            return;
        }

        bool ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool alt   = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        int vk = KeyInterop.VirtualKeyFromKey(wpfKey);
        bool isFKey = wpfKey >= Key.F1 && wpfKey <= Key.F24;
        // Require a modifier combination OR a function key — otherwise we would
        // swallow ordinary typing.
        if (!ctrl && !alt && !shift && !isFKey) return;

        var binding = new HotkeyService.HotkeyBinding { VirtualKey = vk, Ctrl = ctrl, Alt = alt, Shift = shift };

        var conflict = hk.FindConflict(_capturingHotkeyAction.Value, binding);
        if (conflict != null)
        {
            var conflictName = conflict.Value switch
            {
                HotkeyService.HotkeyAction.TogglePower => L.HotkeyToggleLabel,
                HotkeyService.HotkeyAction.ToggleMute  => L.HotkeyMuteLabel,
                HotkeyService.HotkeyAction.CyclePreset => L.HotkeyPresetLabel,
                HotkeyService.HotkeyAction.PushToTalk  => L.HotkeyPttLabel,
                _ => conflict.Value.ToString()
            };
            ShowConflictWarning(string.Format(L.HotkeyConflictFmt, conflictName));
            UpdateHotkeyButtonLabels();
            _capturingHotkeyAction = null;
            e.Handled = true;
            return;
        }

        HideConflictWarning();
        hk.RebindAction(_capturingHotkeyAction.Value, binding);
        _capturingHotkeyAction = null;
        UpdateHotkeyButtonLabels();
        e.Handled = true;
    }

    private void ShowConflictWarning(string message)
    {
        if (HotkeyConflictTxt != null) HotkeyConflictTxt.Text = message;
        if (HotkeyConflict != null) HotkeyConflict.Visibility = Visibility.Visible;
    }

    private void HideConflictWarning()
    {
        if (HotkeyConflict != null) HotkeyConflict.Visibility = Visibility.Collapsed;
    }

    private void HotkeyReset_Click(object s, RoutedEventArgs e)
    {
        if (!_uiReady) return;
        App.Hotkeys?.ResetToDefaults();
        HideConflictWarning();
        UpdateHotkeyButtonLabels();
    }

    private void PttModeRadio_Changed(object s, RoutedEventArgs e)
    {
        if (_suppressPttRadioEvents) return;
        if (App.Hotkeys == null) return;
        if (!ReferenceEquals(s, PttHoldToTalkRadio) && !ReferenceEquals(s, PttHoldToMuteRadio)) return;

        App.Hotkeys.PttHoldToMute = ReferenceEquals(s, PttHoldToMuteRadio);
        App.Hotkeys.SaveConfig();
        UpdatePttModeSection();
    }
}
