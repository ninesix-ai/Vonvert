// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Vonvert.Engine.AudioEngine;
using Vonvert.App.UIServices;

namespace Vonvert.App;

public partial class RecordingViewControl : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private static RecordingService? Rec => App.Recording;
    private readonly HashSet<RecordingHistoryItem> _selectedHistoryItems = new();

    // Search / filter state
    private string _recSearchQuery = "";
    private string? _recDateFilter;     // null = All
    private string? _recDurationFilter; // null = All
    private bool _recChipsBuilt;

    /// <summary>MainWindow surface for toast notifications.</summary>
    private IAppServices? AppServices => Window.GetWindow(this) as IAppServices;

    public RecordingViewControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object s, RoutedEventArgs e)
    {
        // Notify the user when a recording file move fails.
        if (Rec != null)
            Rec.RecordingSaveFailed += msg =>
                AppServices?.ShowNotification(msg, "warning");
        RefreshRecordingHistory();
    }

    // ════ Recording ════

    private void StartRecord_Click(object s, RoutedEventArgs e)
    {
        var confirmResult = MessageBox.Show(
            L.RecWarningBody,
            L.RecWarningTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmResult != MessageBoxResult.Yes) return;

        Rec?.StartRecording();
        StartRecordBtn.Visibility = Visibility.Collapsed;
        StopRecordBtn.Visibility = Visibility.Visible;
        CancelRecordBtn.Visibility = Visibility.Visible;
        RecordModePanel.Visibility = Visibility.Visible;
        RecordingStatus.Text = L.RecordingInProgress;
        RecordingStatus.Foreground = FindResource("PowerOff") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Red;
    }

    private void StopRecord_Click(object s, RoutedEventArgs e)
    {
        var item = Rec?.StopRecording();
        StartRecordBtn.Visibility = Visibility.Visible;
        StopRecordBtn.Visibility = Visibility.Collapsed;
        CancelRecordBtn.Visibility = Visibility.Collapsed;
        RecordModePanel.Visibility = Visibility.Collapsed;
        if (Rec != null) Rec.RecordMode = RecordMode.Processed;
        RecordingStatus.Text = "";
        if (item != null)
        {
            RefreshRecordingHistory();
            AppServices?.ShowNotification(L.RecordingSaved, "success");
        }
    }

    private void CancelRecord_Click(object s, RoutedEventArgs e)
    {
        Rec?.CancelRecording();
        StartRecordBtn.Visibility = Visibility.Visible;
        StopRecordBtn.Visibility = Visibility.Collapsed;
        CancelRecordBtn.Visibility = Visibility.Collapsed;
        RecordModePanel.Visibility = Visibility.Collapsed;
        if (Rec != null) Rec.RecordMode = RecordMode.Processed;
        RecordingStatus.Text = "";
    }

    private void RecordModeProcessed_Click(object s, RoutedEventArgs e)
    {
        if (Rec != null) Rec.RecordMode = RecordMode.Processed;
        RecordingStatus.Text = L.RecordModeProcessed;
    }

    private void RecordModeOriginal_Click(object s, RoutedEventArgs e)
    {
        if (Rec != null) Rec.RecordMode = RecordMode.Original;
        RecordingStatus.Text = L.RecordModeOriginal;
    }

    private void ExportRecording_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button btn || btn.Tag is not RecordingHistoryItem item) return;
        var exportDlg = new ExportDialog(item, L);
        exportDlg.Owner = Window.GetWindow(this);
        if (exportDlg.ShowDialog() == true)
        {
            try
            {
                bool ok = Rec?.ExportRecording(item, exportDlg.OutputPath, exportDlg.Options) ?? false;
                if (ok) AppServices?.ShowNotification(L.ExportSuccess2, "success");
                else AppServices?.ShowNotification(L.ExportFailed, "error");
            }
            catch { AppServices?.ShowNotification(L.ExportFailed, "error"); }
        }
    }

    private void DeleteRecording_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button btn || btn.Tag is not RecordingHistoryItem item) return;
        var result = MessageBox.Show(L.DeleteConfirmMsg, L.DeleteConfirm, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            if (Rec?.DeleteRecording(item) == true)
            {
                _selectedHistoryItems.Remove(item);
                RefreshRecordingHistory();
            }
        }
    }

    // ── Batch selection / delete ────────────────────────────────────

    private void SelectAll_Checked(object s, RoutedEventArgs e)
    {
        _selectedHistoryItems.Clear();
        if (Rec != null)
            foreach (var item in Rec.History)
                _selectedHistoryItems.Add(item);
        UpdateDeleteSelectedState();
    }

    private void SelectAll_Unchecked(object s, RoutedEventArgs e)
    {
        _selectedHistoryItems.Clear();
        UpdateDeleteSelectedState();
    }

    private void HistoryItem_Checked(object s, RoutedEventArgs e)
    {
        if (s is CheckBox cb && cb.Tag is RecordingHistoryItem item)
            _selectedHistoryItems.Add(item);
        UpdateDeleteSelectedState();
    }

    private void HistoryItem_Unchecked(object s, RoutedEventArgs e)
    {
        if (s is CheckBox cb && cb.Tag is RecordingHistoryItem item)
            _selectedHistoryItems.Remove(item);
        // Uncheck "Select All" if not all items are selected
        if (SelectAllCheckbox.IsChecked == true)
            SelectAllCheckbox.IsChecked = false;
        UpdateDeleteSelectedState();
    }

    private void UpdateDeleteSelectedState()
    {
        DeleteSelectedBtn.IsEnabled = _selectedHistoryItems.Count > 0;
    }

    private void DeleteSelected_Click(object s, RoutedEventArgs e)
    {
        if (_selectedHistoryItems.Count == 0)
        {
            AppServices?.ShowNotification(L.NoItemsSelected, "info");
            return;
        }

        var msg = string.Format(L.DeleteSelectedConfirmMsg, _selectedHistoryItems.Count);
        var result = MessageBox.Show(msg, L.DeleteConfirm, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        int deleted = Rec?.DeleteRecordings(_selectedHistoryItems) ?? 0;
        _selectedHistoryItems.Clear();
        SelectAllCheckbox.IsChecked = false;
        RefreshRecordingHistory();

        if (deleted > 0)
            AppServices?.ShowNotification($"{L.BatchDone}: {deleted}", "info");
    }

    public void RefreshRecordingHistory()
    {
        _selectedHistoryItems.Clear();
        if (SelectAllCheckbox != null) SelectAllCheckbox.IsChecked = false;
        if (!_recChipsBuilt)
        {
            BuildRecordingFilterChips();
            _recChipsBuilt = true;
        }
        ApplyRecordingFilter();
        RefreshStats();
    }

    public void RefreshStats()
    {
        if (Rec == null || RecStatsText == null) return;
        var history = Rec.History;
        int count = history.Count;
        double totalDuration = history.Sum(h => h.Duration);
        long totalSize = history.Sum(h => h.FileSize);

        string durationStr = totalDuration >= 3600
            ? $"{(int)totalDuration / 3600}h {(int)(totalDuration % 3600) / 60}m"
            : $"{(int)totalDuration / 60}m {(int)totalDuration % 60}s";
        string sizeStr = totalSize >= 1_073_741_824
            ? $"{totalSize / 1_073_741_824.0:F1} GB"
            : $"{totalSize / 1_048_576.0:F1} MB";

        RecStatsText.Text = string.Format(L.RecStatsFormat, count, durationStr, sizeStr);
    }

    // ── Search + Filter ────────────────────────────────────────────────

    private void BuildRecordingFilterChips()
    {
        // Date chips
        var dateChips = new (string? key, string label)[]
        {
            (null,    L.CategoryAll),
            ("today",  L.RecDateToday),
            ("7days",  L.RecDate7Days),
            ("30days", L.RecDate30Days),
        };
        foreach (var (key, label) in dateChips)
        {
            var rb = CreateFilterChip("RecDateFilterGroup", key, label);
            rb.Checked += OnDateFilterChip_Checked;
            RecDateFilterPanel.Children.Add(rb);
        }

        // Duration chips
        var durChips = new (string? key, string label)[]
        {
            (null,     L.CategoryAll),
            ("under1",  L.RecDurUnder1),
            ("1to5",    L.RecDur1to5),
            ("5to15",   L.RecDur5to15),
            ("over15",  L.RecDurOver15),
        };
        foreach (var (key, label) in durChips)
        {
            var rb = CreateFilterChip("RecDurFilterGroup", key, label);
            rb.Checked += OnDurationFilterChip_Checked;
            RecDurationFilterPanel.Children.Add(rb);
        }
    }

    private RadioButton CreateFilterChip(string group, string? key, string label)
    {
        var rb = new RadioButton
        {
            Content = label,
            GroupName = group,
            Tag = key,
            IsChecked = key == null,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 10,
            Padding = new Thickness(8, 3, 8, 3),
            Foreground = FindResource("TextSub") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gray,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        return rb;
    }

    private void OnRecordingSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _recSearchQuery = RecSearchBox.Text;
        ApplyRecordingFilter();
    }

    private void OnDateFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
            _recDateFilter = rb.Tag as string;
        ApplyRecordingFilter();
    }

    private void OnDurationFilterChip_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
            _recDurationFilter = rb.Tag as string;
        ApplyRecordingFilter();
    }

    private void ApplyRecordingFilter()
    {
        if (RecordingHistoryList == null) return;
        var now = DateTime.Now;
        var history = Rec?.History ?? new List<RecordingHistoryItem>();
        var filtered = history.AsEnumerable();

        // Name search
        if (!string.IsNullOrWhiteSpace(_recSearchQuery))
        {
            var q = _recSearchQuery.Trim();
            filtered = filtered.Where(h => h.FileName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Date filter
        if (_recDateFilter == "today")
            filtered = filtered.Where(h => h.CreatedAt.Date == now.Date);
        else if (_recDateFilter == "7days")
            filtered = filtered.Where(h => h.CreatedAt >= now.AddDays(-7));
        else if (_recDateFilter == "30days")
            filtered = filtered.Where(h => h.CreatedAt >= now.AddDays(-30));

        // Duration filter (seconds)
        if (_recDurationFilter == "under1")
            filtered = filtered.Where(h => h.Duration < 60);
        else if (_recDurationFilter == "1to5")
            filtered = filtered.Where(h => h.Duration >= 60 && h.Duration < 300);
        else if (_recDurationFilter == "5to15")
            filtered = filtered.Where(h => h.Duration >= 300 && h.Duration < 900);
        else if (_recDurationFilter == "over15")
            filtered = filtered.Where(h => h.Duration >= 900);

        var result = filtered.ToList();
        RecordingHistoryList.ItemsSource = result;
        if (RecordingEmptyHint != null)
            RecordingEmptyHint.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateDeleteSelectedState();
    }
}
