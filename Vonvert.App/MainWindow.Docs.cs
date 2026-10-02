// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Diagnostics;
using System.Windows;
using Vonvert.App.UIServices;
using Vonvert.Engine;

namespace Vonvert.App;

/// <content>
/// Help links on the About tab. Both open the published guide in the user's browser, in
/// the language they picked. The installer ships no local copy of the docs, and a raw
/// markdown file rendered in a browser is not a help page for this audience - so the
/// links always go to the web.
/// </content>
public partial class MainWindow
{
    private void UserGuideLink_Click(object s, RoutedEventArgs e)
        => OpenDocs(DocsLinks.UserGuideUrl(LocalizationManager.Instance.Language));

    private void MonitorGuideLink_Click(object s, RoutedEventArgs e)
        => OpenDocs(DocsLinks.MonitorGuideUrl(LocalizationManager.Instance.Language));

    /// <summary>Best-effort hand-off to the default browser; a machine without a browser
    /// association must not take the window down with it.</summary>
    private void OpenDocs(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) { AppLog.Warning(ex, "[MainWindow] could not open the docs page"); }
    }
}
