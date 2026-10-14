// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

/// <summary>
/// Settings "Data location" card: shows the active user-data root and lets the
/// user relocate it. The change is persisted via <see cref="AppPaths.TrySetRoot"/>
/// (a pointer file under the immovable default root) and takes effect on the next
/// launch; the user is offered a one-time copy of existing data to the new spot.
/// </summary>
public partial class MainWindow
{
    /// <summary>Reflect the currently active data root in the Settings card.</summary>
    private void InitDataLocationView()
    {
        if (DataRootText != null) DataRootText.Text = AppPaths.Root;
    }

    private void DataLocation_Changed(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.ChangeLocation };
        try { dlg.InitialDirectory = AppPaths.Root; } catch { /* best-effort preselect */ }
        if (dlg.ShowDialog() != true) return;

        var newRoot = dlg.FolderName;
        if (!AppPaths.IsUsablePath(newRoot))
        {
            ShowToast(L.DataLocationInvalid, "error");
            return;
        }

        var oldRoot = AppPaths.Root;
        if (PathsEqual(oldRoot, newRoot)) return;   // no change

        // Offer to carry existing data over so the user does not start from empty.
        var answer = MessageBox.Show(
            string.Format(L.MigrateDataPromptFmt, oldRoot),
            L.DataLocation, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes && !ShouldSkipCopy(oldRoot, newRoot))
        {
            try { CopyDirectory(oldRoot, newRoot); }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "Data-location migration failed");
                ShowToast(L.DataMigrationFailed, "error");
                return;   // do not repoint if the copy failed
            }
        }

        if (!AppPaths.TrySetRoot(newRoot))
        {
            ShowToast(L.DataLocationInvalid, "error");
            return;
        }
        ShowToast(L.DataLocationChangedRestart, "success");
    }

    /// <summary>Bundle app logs + crash + a PII-light diagnostics snapshot into a
    /// user-chosen zip (Settings > Data location). Never touches the live engine.</summary>
    private void LogsExport_Click(object s, RoutedEventArgs e)
    {
        var crash = Path.Combine(Path.GetTempPath(), "Vonvert_crash.txt");
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.ExportLogs,
            FileName = $"vonvert-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            DefaultExt = "zip",
            AddExtension = true,
            Filter = "Zip archive (*.zip)|*.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            int count = Directory.Exists(AppLog.LogDirectory)
                ? Directory.GetFiles(AppLog.LogDirectory, "*.log").Length : 0;
            string appVer = VersionInfoService.GetShortVersion();
            string engineVer = typeof(AppLog).Assembly.GetName().Version?.ToString() ?? "unknown";
            var diag = LogExportService.BuildDiagnosticsText(appVer, engineVer, count);

            if (LogExportService.ExportBundle(dlg.FileName, AppLog.LogDirectory, crash, diag))
                ShowToast(L.LogsExportOk, "success");
            else
                ShowToast(L.LogsExportEmpty, "error");
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "Diagnostic log export failed");
            ShowToast(L.LogsExportFailed, "error");
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // Copying a folder into itself (or a descendant) would recurse endlessly.
    private static bool ShouldSkipCopy(string source, string dest)
    {
        try
        {
            var s = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            var d = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dest));
            return d.StartsWith(s + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, d, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
