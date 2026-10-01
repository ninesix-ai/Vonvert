// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using Vonvert.Engine;

namespace Vonvert.App;

/// <summary>
/// Fullscreen professional monitor lifecycle: idempotent open from the header
/// tool row, and deferred close so the child window never tears down inside
/// the main window's OnClosing sequence (avoids re-entrancy and blocking when
/// the app is shutting down).
/// </summary>
public partial class MainWindow
{
    private FullscreenVisualizationWindow? _fullscreenViz;

    private void FullscreenVisualizer_Click(object sender, RoutedEventArgs e)
    {
        if (_fullscreenViz != null && _fullscreenViz.IsLoaded)
        {
            _fullscreenViz.Activate();
            return;
        }
        try
        {
            _fullscreenViz = new FullscreenVisualizationWindow();
            if (!string.IsNullOrEmpty(_selectedPresetName))
                _fullscreenViz.ShowPresetName(_selectedPresetName);
            _fullscreenViz.Closed += (_, _) => _fullscreenViz = null;
            _fullscreenViz.Show();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "[MainWindow] opening the fullscreen monitor failed");
            ShowNotification(ex.Message, "error");
            _fullscreenViz = null;
        }
    }

    /// <summary>Close the monitor after the main window's own closing sequence yields.</summary>
    private void CloseFullscreenVizDeferred()
    {
        var viz = _fullscreenViz;
        if (viz == null) return;
        _fullscreenViz = null;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { viz.Close(); } catch { /* monitor close is best-effort */ }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
