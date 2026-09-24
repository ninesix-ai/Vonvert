// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows;
using System.Windows.Controls;

namespace Vonvert.App.Controls;

/// <summary>
/// Left navigation rail — logo + Voice Change / Settings nav buttons.
/// Tab switching must be bound to Checked (not Click) so keyboard focus and
/// UI-automation selection trigger navigation too. Programmatic selection sync
/// is guarded by <see cref="_syncing"/> to prevent Checked/Tab recursion.
/// </summary>
public partial class SidebarNavRail : UserControl
{
    /// <summary>Fired with the target tab index (0 = Voices, 1 = Settings, 2 = About, 3 = Recording) when the user checks a nav button.</summary>
    public event Action<int>? TabRequested;

    private bool _syncing;

    public SidebarNavRail()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Programmatically select a tab (called on MainTabs.SelectionChanged) without
    /// raising <see cref="TabRequested"/> (avoids the check → switch → re-check loop).
    /// </summary>
    public void SetSelected(int index)
    {
        if (index < 0 || index > 3) return;
        _syncing = true;
        try
        {
            var button = index == 0 ? NavVoicesBtn
                       : index == 1 ? NavSettingsBtn
                       : index == 2 ? NavAboutBtn
                       : NavRecordingBtn;
            if (button != null && button.IsChecked != true) button.IsChecked = true;
        }
        finally
        {
            _syncing = false;
        }
    }

    // Checked-driven (NOT Click): fires for mouse clicks, Space/Enter key
    // activation and UI-automation Select — all of them must switch the tab.
    private void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        if (sender is not RadioButton btn || btn.IsChecked != true) return;

        int target = btn == NavVoicesBtn    ? 0
                   : btn == NavSettingsBtn  ? 1
                   : btn == NavAboutBtn     ? 2
                   : btn == NavRecordingBtn ? 3
                   : -1;
        if (target < 0) return;
        TabRequested?.Invoke(target);
    }
}