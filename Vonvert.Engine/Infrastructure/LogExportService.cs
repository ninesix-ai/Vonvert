// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Vonvert.Engine;

/// <summary>
/// Bundles application logs plus a PII-light diagnostics snapshot into a single
/// zip the user can hand to support. Pure file IO with injected paths, so it is
/// unit-testable without touching the real user profile. UI-free by design.
/// </summary>
public static class LogExportService
{
    /// <summary>
    /// Collect <c>*.log</c> from <paramref name="logDir"/> (plus an optional crash
    /// file) into <paramref name="destZip"/>, adding <c>diagnostics.txt</c>.
    /// Returns false when there is nothing to export (no zip is written then).
    /// Individual locked files (Serilog may be mid-roll) are skipped, not fatal.
    /// </summary>
    public static bool ExportBundle(string destZip, string logDir, string? crashFile, string diagnosticsText)
    {
        var logs = Directory.Exists(logDir) ? Directory.GetFiles(logDir, "*.log") : Array.Empty<string>();
        bool hasCrash = !string.IsNullOrEmpty(crashFile) && File.Exists(crashFile);
        if (logs.Length == 0 && !hasCrash) return false;

        if (File.Exists(destZip)) File.Delete(destZip);

        using var zip = ZipFile.Open(destZip, ZipArchiveMode.Create);
        foreach (var file in logs)
        {
            try { zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal); }
            catch (IOException) { /* locked during rolling — skip, best-effort */ }
        }
        if (hasCrash)
        {
            try { zip.CreateEntryFromFile(crashFile!, "crash.txt", CompressionLevel.Optimal); }
            catch (IOException) { /* crash file locked — skip */ }
        }

        var entry = zip.CreateEntry("diagnostics.txt", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(entry.Open()))
        {
            writer.Write(diagnosticsText);
        }
        return true;
    }

    /// <summary>
    /// Build the diagnostics snapshot. Deliberately excludes any identity or
    /// absolute path data (username, machine name, log directory), matching the
    /// privacy stance already taken in <see cref="AppLog"/>.
    /// </summary>
    public static string BuildDiagnosticsText(string appVersion, string engineVersion, int logFileCount)
    {
        var os = Environment.OSVersion.Version;
        return
            "=== Vonvert Diagnostic Info ===\n" +
            $"Exported UTC:    {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n" +
            $"App version:     {appVersion}\n" +
            $"Engine version:  {engineVersion}\n" +
            $"OS:              Windows {os.Major}.{os.Minor}\n" +
            $"Runtime:         {RuntimeInformation.FrameworkDescription}\n" +
            $"Log files:       {logFileCount}\n";
    }
}
