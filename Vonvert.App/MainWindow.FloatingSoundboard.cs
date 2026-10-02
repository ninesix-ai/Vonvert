// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using Vonvert.App.UIServices;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Floating soundboard lifecycle + settings/header toggle sync. The window's existence is the
/// single source of truth; both the settings "Show" toggle and the Soundboard-tab header toggle
/// are projections that reflect and command it. Closing the window deliberately also cancels
/// auto-open, so "auto-open" only means "open when the app cold-starts" until the user opts back in.
/// </summary>
public partial class MainWindow
{
    private FloatingSoundboardWindow? _floatingSoundboard;

    /// <summary>Raised whenever the floating window opens (true) or closes (false), so any bound
    /// toggle (settings card, Soundboard-tab header) mirrors the real window state.</summary>
    internal event Action<bool>? FloatingVisibilityChanged;

    /// <summary>Whether the floating mini-player is currently shown.</summary>
    internal bool IsFloatingOpen => _floatingSoundboard is { IsLoaded: true };

    /// <summary>Show or hide the floating window (idempotent per target state).</summary>
    internal void SetFloatingVisible(bool visible)
    {
        if (visible) { if (!IsFloatingOpen) ShowFloatingSoundboard(); }
        else if (_floatingSoundboard != null) _floatingSoundboard.Close();
    }

    private void ShowFloatingSoundboard()
    {
        try
        {
            var w = new FloatingSoundboardWindow { Owner = this };
            w.Closed += (_, _) => OnFloatingClosed(w);
            _floatingSoundboard = w;
            w.Show();
            FloatingVisibilityChanged?.Invoke(true);
            SyncFloatingShowToggle(true);
        }
        catch (Exception ex)
        {
            // Details to the log; the user gets a single actionable sentence (mirrors the
            // fullscreen window's error path rather than leaking an untranslated stack string).
            AppLog.Error(ex, "[MainWindow] opening the floating soundboard failed");
            ShowNotification(LocalizationManager.Instance.FloatingTitle, "error");
            _floatingSoundboard = null;
        }
    }

    /// <summary>Window closed (its own ✕ or a toggle): mirror state, and — being a deliberate
    /// user action — cancel auto-open so it will not silently reappear on the next launch.</summary>
    private void OnFloatingClosed(FloatingSoundboardWindow w)
    {
        if (ReferenceEquals(_floatingSoundboard, w)) _floatingSoundboard = null;
        FloatingVisibilityChanged?.Invoke(false);
        SyncFloatingShowToggle(false);

        AppConfig.Instance.Audio.FloatAutoOpen = false;
        AppConfig.Instance.Save();
        SyncFloatingAutoOpenToggle(false);
    }

    /// <summary>Auto-open when the app cold-starts, only if opted in and at least one pad is pinned.</summary>
    internal void MaybeAutoOpenFloatingSoundboard()
    {
        if (!AppConfig.Instance.Audio.FloatAutoOpen) return;
        if (App.Soundboard is null || App.Soundboard.PinnedSoundIds.Count == 0) return;
        if (IsFloatingOpen) return;
        ShowFloatingSoundboard();
    }

    // ── Settings-card + header toggle wiring ──
    // This guard suppresses handlers while we programmatically mirror state into a toggle, so
    // reflecting (never commanding) a toggle cannot recurse or rewrite config.
    private bool _syncingFloatToggles;

    /// <summary>Sync the three settings toggles to persisted config / live window state on tab load.</summary>
    private void InitFloatingSoundboardToggles()
    {
        SyncFloatingAutoOpenToggle(AppConfig.Instance.Audio.FloatAutoOpen);
        SyncFloatingTopmostToggle(AppConfig.Instance.Audio.FloatTopmost);
        SyncFloatingShowToggle(IsFloatingOpen);
    }

    private void SyncFloatingShowToggle(bool open)
        => SetToggle(FloatingShowToggle, open);
    private void SyncFloatingAutoOpenToggle(bool on)
        => SetToggle(FloatingAutoOpenToggle, on);
    private void SyncFloatingTopmostToggle(bool on)
        => SetToggle(FloatingTopmostToggle, on);

    private void SetToggle(ToggleButton? toggle, bool value)
    {
        if (toggle == null) return;   // Settings tab is lazily realized; may not exist yet.
        _syncingFloatToggles = true;
        try { toggle.IsChecked = value; }
        finally { _syncingFloatToggles = false; }
    }

    private void FloatingShow_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingFloatToggles) return;
        SetFloatingVisible((sender as ToggleButton)?.IsChecked == true);
    }

    private void FloatingAutoOpen_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingFloatToggles) return;
        bool on = (sender as ToggleButton)?.IsChecked == true;
        AppConfig.Instance.Audio.FloatAutoOpen = on;
        AppConfig.Instance.Save();
        if (on) MaybeAutoOpenFloatingSoundboard();
    }

    private void FloatingTopmost_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingFloatToggles) return;
        bool top = (sender as ToggleButton)?.IsChecked == true;
        AppConfig.Instance.Audio.FloatTopmost = top;
        AppConfig.Instance.Save();
        if (_floatingSoundboard != null) _floatingSoundboard.Topmost = top;
    }
}
