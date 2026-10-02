// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Windows;
using Vonvert.App.UIServices;
using Vonvert.Engine;

namespace Vonvert.App;

/// <summary>
/// Fullscreen professional monitor lifecycle: idempotent open from the header
/// tool row. The window is owned by the main window, so WPF closes it as part
/// of the main window's own shutdown — no dispatcher-priority tricks (a
/// deferred Background close would be dropped by Application.Shutdown, which
/// queues at Normal priority).
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
            var viz = new FullscreenVisualizationWindow { Owner = this };
            if (!string.IsNullOrEmpty(_selectedPresetName))
                viz.ShowPresetName(_selectedPresetName);
            viz.Closed += (_, _) => { if (ReferenceEquals(_fullscreenViz, viz)) _fullscreenViz = null; };
            _fullscreenViz = viz;
            viz.Show();
        }
        catch (Exception ex)
        {
            // Details go to the log; the user gets a sentence they can act on. Passing
            // ex.Message straight through produced an untranslated technical string with
            // no next step, which is how "the app just errors" support tickets start.
            AppLog.Error(ex, "[MainWindow] opening the fullscreen monitor failed");
            ShowNotification(LocalizationManager.Instance.FsErrOpenFailed, "error");
            _fullscreenViz = null;
        }
    }
}
