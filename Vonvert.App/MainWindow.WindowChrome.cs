// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.ComponentModel;

namespace Vonvert.App;

public partial class MainWindow
{
    // ════ Window chrome ════

    private void Header_Drag(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        DragMove();
    }
    private void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object s, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            RootBorder.CornerRadius = new System.Windows.CornerRadius(16);
            HeaderBorder.CornerRadius = new System.Windows.CornerRadius(16, 16, 0, 0);
            if (BottomBar?.OuterBorder != null) BottomBar.OuterBorder.CornerRadius = new System.Windows.CornerRadius(0, 0, 16, 16);
        }
        else
        {
            WindowState = WindowState.Maximized;
            RootBorder.CornerRadius = new System.Windows.CornerRadius(0);
            HeaderBorder.CornerRadius = new System.Windows.CornerRadius(0);
            if (BottomBar?.OuterBorder != null) BottomBar.OuterBorder.CornerRadius = new System.Windows.CornerRadius(0);
        }
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        MaximizeIcon.Source = WindowState == WindowState.Maximized
            ? FindResource("VM_Restore") as System.Windows.Media.ImageSource
            : FindResource("VM_Maximize") as System.Windows.Media.ImageSource;
    }

    private void Close_Click(object s, RoutedEventArgs e)
    {
        // X button always exits.
        Close();
    }

    /// <summary>
    /// Release external service resources owned by the MainWindow. Called from
    /// App.OnExit as a safety net when the app exits via a path that bypasses
    /// OnClosing. Idempotent — safe to call multiple times.
    /// </summary>
    internal void CleanupExternalServices()
    {
        try { _updateChecker.Stop(); } catch { /* update checker stop is best-effort */ }
        try { App.Hotkeys?.Dispose(); } catch { /* hotkey service dispose is best-effort */ }
    }

    // ════ Edge-resize support (WM_NCHITTEST) ════

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromDependencyObject(this) as HwndSource;
        _hwndSource?.AddHook(WmNcHitTest);
    }

    private const int WM_NCHITTEST = 0x0084;

    private IntPtr WmNcHitTest(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // CRITICAL: Only process WM_NCHITTEST. If we touch any other window message
        // (WM_PAINT, WM_ERASEBKGND, WM_SETTEXT, WM_NCCALCSIZE, etc.) WPF's
        // composition target silently breaks and the entire window renders
        // blank/black with an empty visual tree.
        if (msg != WM_NCHITTEST) return IntPtr.Zero;

        // Convert screen coords to WPF device-independent coords
        var screenPoint = new System.Windows.Point(
            (short)(lParam.ToInt64() & 0xFFFF),
            (short)((lParam.ToInt64() >> 16) & 0xFFFF));
        var wpfPoint = PointFromScreen(screenPoint);

        double w = ActualWidth;
        double h = ActualHeight;

        bool top    = wpfPoint.Y < ResizeBorderWidth;
        bool bottom = wpfPoint.Y >= h - ResizeBorderWidth;
        bool left   = wpfPoint.X < ResizeBorderWidth;
        bool right  = wpfPoint.X >= w - ResizeBorderWidth;

        if (top && left)       { handled = true; return (IntPtr)13; } // HTTOPLEFT
        if (top && right)      { handled = true; return (IntPtr)14; } // HTTOPRIGHT
        if (bottom && left)    { handled = true; return (IntPtr)16; } // HTBOTTOMLEFT
        if (bottom && right)   { handled = true; return (IntPtr)17; } // HTBOTTOMRIGHT
        if (top)               { handled = true; return (IntPtr)12; } // HTTOP
        if (bottom)            { handled = true; return (IntPtr)15; } // HTBOTTOM
        if (left)              { handled = true; return (IntPtr)10; } // HTLEFT
        if (right)             { handled = true; return (IntPtr)11; } // HTRIGHT

        return IntPtr.Zero;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizeIcon();

        // Sync corner radius and shadow with window state
        if (RootBorder == null) return;
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.CornerRadius = new System.Windows.CornerRadius(0);
            if (HeaderBorder != null) HeaderBorder.CornerRadius = new System.Windows.CornerRadius(0);
            if (BottomBar?.OuterBorder != null) BottomBar.OuterBorder.CornerRadius = new System.Windows.CornerRadius(0);
        }
        else
        {
            RootBorder.CornerRadius = new System.Windows.CornerRadius(16);
            if (HeaderBorder != null) HeaderBorder.CornerRadius = new System.Windows.CornerRadius(16, 16, 0, 0);
            if (BottomBar?.OuterBorder != null) BottomBar.OuterBorder.CornerRadius = new System.Windows.CornerRadius(0, 0, 16, 16);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // No minimize-to-tray option — close exits directly.
        // No blocking confirmation: the tray icon plus toast feedback covers
        // the "still running" case.
        _metricsTimer.Stop();
        CleanupForExit();
        base.OnClosing(e);
    }

    /// <summary>
    /// Orderly shutdown of all UI-level resources.
    /// Must run BEFORE App.OnExit so that event handlers are detached
    /// while the Dispatcher is still fully operational.
    /// Guarded against double-execution.
    /// </summary>
    private void CleanupForExit()
    {
        if (_cleanupDone) return;
        _cleanupDone = true;

        // 1. Stop all timers first — they access Engine/Processor on every tick
        _metricsTimer.Stop();

        // 2. Prevent all event-driven re-entry during shutdown
        _servicesInitialized = false;

        // 3. Detach device-change handlers so Dispose-time COM callbacks
        //    cannot re-enter the UI via Dispatcher.InvokeAsync
        if (App.Devices != null)
            App.Devices.DevicesChanged -= OnDeviceListChangedWrapper;

        // 4. Detach engine event handlers
        if (App.Engine != null)
            App.Engine.DeviceLost -= OnEngineDeviceDisconnectedWrapper;

        // 4c. Detach update checker handler
        _updateChecker.UpdateAvailable -= OnUpdateAvailable;

        // 4d. Detach the central language-change hook
        LocalizationManager.Instance.PropertyChanged -= OnLanguagePropertyChanged;

        // 5. Dispose tray icon (removes system-tray icon)
        TrayIcon?.Dispose();
        CleanupExternalServices();

        // NOTE: App.Engine?.Stop() is intentionally NOT called here.
        // Stop() uses Task.Run().Wait() internally for each WASAPI object (up to 2s each),
        // which blocks the UI thread and can cause visible hangs during close.
        // The engine is stopped by App.OnExit() which runs the entire teardown
        // pipeline on a background task with a 10s ceiling.
    }

    // ════ Navigation Tabs ════

    /// <summary>Sidebar nav selection → TabControl (invoked on Checked, not Click).</summary>
    private void OnNavTabRequested(int index)
    {
        if (MainTabs == null || MainTabs.SelectedIndex == index) return;
        MainTabs.SelectedIndex = index;
    }

    /// <summary>
    /// Keep the section title in sync with the active tab: the selected preset
    /// name on the Voices tab, or the Settings title on the Settings tab.
    /// Also mirrors the active tab back onto the sidebar nav buttons.
    /// </summary>
    private void MainTabs_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (SectionTitle == null || !_uiReady || MainTabs == null) return;
        // Tab-scoped header chrome (A/B compare): a single declarative,
        // fail-closed policy decides which shared-header controls are visible per
        // tab, so a new tab or a new header widget can never leave a stale
        // imperative check that lets a control leak onto the wrong tab.
        if (TabChromePolicy.TryFromIndex(MainTabs.SelectedIndex, out var activeTab))
        {
            ApplyChrome(TabChromePolicy.HeaderControl.AbCompare, activeTab, ABComparePanel);
        }
        else
        {
            // Unknown tab index → fail-closed: hide all tab-scoped chrome.
            if (ABComparePanel != null) ABComparePanel.Visibility = Visibility.Collapsed;
        }
        SidebarNav?.SetSelected(MainTabs.SelectedIndex);
        RefreshSectionTitle();
    }

    /// <summary>Set the section header title for the active tab (Voices keeps the selected
    /// preset's display name). Localized — must be re-run when the language changes.</summary>
    private void RefreshSectionTitle()
    {
        if (SectionTitle == null || MainTabs == null) return;
        switch (MainTabs.SelectedIndex)
        {
            case 1: SectionTitle.Text = L.Settings; break;
            case 2: SectionTitle.Text = L.About; break;
            case 3: SectionTitle.Text = L.RecordingTitle; break;
            case 0 when _selectedPresetName != null: SectionTitle.Text = _selectedPresetName; break;
        }
    }

    /// <summary>
    /// Central language-change hook. XAML-bound strings refresh themselves via
    /// LocalizationManager.PropertyChanged, but text assigned imperatively in code
    /// (e.g. <c>PttTxt.Text = L.Foo</c>) does NOT. This one handler re-runs every
    /// imperative refresher so switching language can never strand a control in the
    /// previous language — the class of bug where the PTT hold-mode button stayed
    /// untranslated after an en↔zh switch.
    /// </summary>
    private void OnLanguagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocalizationManager.Language)) return;   // fire once per switch
        if (!_uiReady) return;
        UpdateDynamicStrings();
        UpdateTrayMenuStrings();
        UpdateHotkeyButtonLabels();          // includes the PTT hold-mode button text
        if (MainPresetPicker?.SelectedPreset is { } sp)
        {
            _selectedPresetName = L.GetPresetDisplayName(sp.Name);
            MainPresetPicker.RefreshTiles(); // re-localize preset tiles, keeping the current selection
        }
        ExpertPanel?.Relabel();              // re-translate the data-driven expert panel labels
        RefreshSectionTitle();               // active tab's title in the new language
    }

    /// <summary>Apply <see cref="TabChromePolicy"/> to one shared-header element.</summary>
    private static void ApplyChrome(TabChromePolicy.HeaderControl control, TabChromePolicy.AppTab tab, UIElement? element)
    {
        if (element == null) return;
        element.Visibility = TabChromePolicy.IsControlVisible(control, tab)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ════ About tab (version info) ════

    private string _versionInfoText = "";
    private System.Windows.Threading.DispatcherTimer? _copyResetTimer;

    /// <summary>Populate the About tab lazily on first display.</summary>
    private void TabAbout_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_versionInfoText)) return;
        try
        {
            _versionInfoText = VersionInfoService.GetVersionInfoText();
            if (VersionInfoText != null) VersionInfoText.Text = _versionInfoText;
            if (AboutVersionLine != null) AboutVersionLine.Text = "v" + VersionInfoService.GetShortVersion();
        }
        catch (Exception ex) { AppLog.Warning(ex, "About version info failed to load"); }
    }

    private void AboutCopy_Click(object sender, RoutedEventArgs e)
    {
        if (AboutCopyBtn == null) return;
        try
        {
            System.Windows.Clipboard.SetText(_versionInfoText);
            AboutCopyBtn.Content = L.Copied;

            // Reset the button label back to "Copy all" after 2 s.
            _copyResetTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _copyResetTimer.Stop();
            _copyResetTimer.Tick -= CopyResetTimer_Tick;
            _copyResetTimer.Tick += CopyResetTimer_Tick;
            _copyResetTimer.Start();
        }
        catch (Exception ex)
        {
            ShowNotification(string.Format(L.CopyFailed, ex.Message), "error");
        }
    }

    private void CopyResetTimer_Tick(object? sender, EventArgs e)
    {
        _copyResetTimer?.Stop();
        if (AboutCopyBtn != null)
            AboutCopyBtn.Content = LocalizationManager.Instance.CopyAll;
    }

    // ── Update Checker ────────────────────────────────────────────────────

    /// <summary>Called on the UI thread when a newer version is found on GitHub.</summary>
    private void OnUpdateAvailable(string latestTag, string releaseUrl)
    {
        try
        {
            if (BottomBar?.UpdateText == null || BottomBar.Update == null) return;
            BottomBar.UpdateText.Text = $"\u2192 {latestTag}";
            BottomBar.Update.ToolTip = string.Format(L.UpdateBadgeTooltipFmt, latestTag);
            BottomBar.Update.Visibility = Visibility.Visible;
            AppLog.Information("UpdateChecker: new version available → {Tag}", latestTag);
        }
        catch (Exception ex) { AppLog.Warning(ex, "UpdateChecker: failed to show update badge"); }
    }

    // ════ Dynamic strings (language refresh) ════

    private void UpdateDynamicStrings()
    {
        UpdateVoiceToggleVisuals(_isEffectOn);
        if (BottomBar?.Latency != null) BottomBar.Latency.Text  = L.LatencyDash;
        if (BottomBar?.Status != null) BottomBar.Status.Text   = L.Stopped;
        CheckVBCable();

        // Refresh localized app name in title and tray tooltip
        try
        {
            Title = L.AppName;
            if (TrayIcon != null) TrayIcon.ToolTipText = L.AppName;
        }
        catch { /* title bar update is best-effort */ }
    }

    private void UpdateTrayMenuStrings()
    {
        TrayOpenItem.Header  = string.Format(L.TrayOpen, L.AppName);
        TrayQuitItem.Header  = L.TrayQuit;
    }

    // ════ System Tray ════

    private void TrayIcon_DoubleClick(object s, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayOpen_Click(object s, RoutedEventArgs e) => TrayIcon_DoubleClick(s, e);

    private void TrayQuit_Click(object s, RoutedEventArgs e)
    {
        Close();
    }
}
