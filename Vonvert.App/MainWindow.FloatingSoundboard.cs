// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using Vonvert.App.UIServices;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Floating soundboard lifecycle: idempotent open/close owned by the main window so WPF
/// tears it down with the app (mirrors MainWindow.Fullscreen.cs — no dispatcher tricks).
/// Also hosts the settings-card handlers that persist the window state.
/// </summary>
public partial class MainWindow
{
    private FloatingSoundboardWindow? _floatingSoundboard;

    /// <summary>Toggle action used by the Soundboard-tab header button and the settings card.</summary>
    internal void ToggleFloatingSoundboard()
    {
        if (_floatingSoundboard is { IsLoaded: true }) { _floatingSoundboard.Close(); return; }
        ShowFloatingSoundboard();
    }

    private void ShowFloatingSoundboard()
    {
        try
        {
            var w = new FloatingSoundboardWindow { Owner = this };
            w.Closed += (_, _) => { if (ReferenceEquals(_floatingSoundboard, w)) _floatingSoundboard = null; };
            _floatingSoundboard = w;
            w.Show();
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

    /// <summary>Auto-open on startup when the user opted in and at least one pad is pinned.</summary>
    internal void MaybeAutoOpenFloatingSoundboard()
    {
        if (!AppConfig.Instance.Audio.FloatAutoOpen) return;
        if (App.Soundboard is null || App.Soundboard.PinnedSoundIds.Count == 0) return;
        if (_floatingSoundboard is { IsLoaded: true }) return;
        ShowFloatingSoundboard();
    }

    // ── Settings-card handlers (wired from MainWindow.xaml) ──
    private void FloatingOpen_Click(object sender, RoutedEventArgs e) => ToggleFloatingSoundboard();

    private void FloatingAutoOpen_Changed(object sender, RoutedEventArgs e)
    {
        bool on = (sender as ToggleButton)?.IsChecked == true;
        AppConfig.Instance.Audio.FloatAutoOpen = on;
        AppConfig.Instance.Save();
        if (on) MaybeAutoOpenFloatingSoundboard();
    }

    private void FloatingTopmost_Changed(object sender, RoutedEventArgs e)
    {
        bool top = (sender as ToggleButton)?.IsChecked == true;
        AppConfig.Instance.Audio.FloatTopmost = top;
        AppConfig.Instance.Save();
        if (_floatingSoundboard != null) _floatingSoundboard.Topmost = top;
    }
}
