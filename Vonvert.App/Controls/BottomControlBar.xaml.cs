// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Vonvert.App.Controls;

/// <summary>
/// Bottom control bar: brand/status label, voice toggle pill, VU meters,
/// latency badge, and update-available badge. Exposes child controls as
/// public read-only properties so MainWindow can still manipulate them
/// directly during the transition period.
/// </summary>
public partial class BottomControlBar : UserControl
{
    /// <summary>Fired when the voice toggle button is clicked.</summary>
    public event Action? EngineToggleRequested;

    /// <summary>Fired when the update badge is clicked.</summary>
    public event Action? UpdateBadgeClicked;

    public BottomControlBar()
    {
        InitializeComponent();
    }

    // ── Public control accessors (transition-period bridge) ─────────────
    // These let MainWindow code continue to work with minimal changes
    // while the controls live inside this UserControl.

    /// <summary>Outer border — used for corner-radius adjustments on maximize/restore.</summary>
    public Border OuterBorder => BottomBorder;

    /// <summary>Status text block (e.g. "Running", "Standby").</summary>
    public SelectableTextBlock Status => StatusLabel;

    /// <summary>VB-Cable detection status text (plain text, shown when a cable is detected).</summary>
    public SelectableTextBlock VbCableStatus => VBCableStatus;

    /// <summary>Clickable "install VB-Cable" link (shown when no virtual cable is detected).</summary>
    public TextBlock VbCableLink => VBCableLink;

    /// <summary>Text run inside the install link (MainWindow sets its localized label).</summary>
    public Run VbCableLinkRun => VBCableLinkText;

    /// <summary>Hyperlink element of the install link (for foreground/color updates).</summary>
    public Hyperlink VbCableHyperlink => VBCableHyperlink;

    /// <summary>Latency display label.</summary>
    public SelectableTextBlock Latency => LatencyLabel;

    /// <summary>Input VU meter bar.</summary>
    public Border InputVU => InputVUBottom;

    /// <summary>Output VU meter bar.</summary>
    public Border OutputVU => OutputVUBottom;

    /// <summary>Voice toggle button (for template access in visual updates).</summary>
    public Button VoiceToggle => VoiceToggleBtn;

    /// <summary>Voice toggle label text.</summary>
    public TextBlock VoiceToggleText => VoiceToggleLabel;

    /// <summary>Update available badge border.</summary>
    public Border Update => UpdateBadge;

    /// <summary>Update badge text.</summary>
    public SelectableTextBlock UpdateText => UpdateBadgeText;

    // ── Internal event handlers ─────────────────────────────────────────

    private void OnEngineToggle(object s, RoutedEventArgs e)
        => EngineToggleRequested?.Invoke();

    private void UpdateBadge_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
        => UpdateBadgeClicked?.Invoke();

    /// <summary>Official VB-Audio download page for the VB-Cable virtual audio driver.</summary>
    private const string VbCableDownloadUrl = "https://vb-audio.com/Cable/";

    /// <summary>
    /// Opens the VB-Cable download page in the user's default browser when the
    /// install link in the status bar is clicked (only reachable when no cable
    /// is detected). Best-effort: a missing browser association must not crash the app.
    /// </summary>
    private void VBCableHyperlink_Click(object s, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = VbCableDownloadUrl,
                UseShellExecute = true
            });
        }
        catch { /* best-effort: opening the download page in a browser */ }
    }
}
