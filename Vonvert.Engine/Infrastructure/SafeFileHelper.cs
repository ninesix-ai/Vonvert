// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine;

/// <summary>
/// Safe file I/O helpers with size-limit guards to prevent memory exhaustion
/// from malicious or corrupted large files.
/// </summary>
public static class SafeFileHelper
{
    /// <summary>Default maximum file size for text/JSON config files (5 MB).</summary>
    public const long DefaultMaxTextFileSize = 5L * 1024 * 1024;

    /// <summary>
    /// Stores the last file I/O error message for diagnostic purposes.
    /// Cleared on each successful operation.
    /// </summary>
    public static string? LastError { get; private set; }

    /// <summary>
    /// Read all text from a file with a size-limit guard.
    /// Rejects files larger than <paramref name="maxBytes"/> to prevent
    /// memory exhaustion from malicious or corrupted files.
    /// </summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <param name="maxBytes">Maximum allowed file size in bytes. Default: 5 MB.</param>
    /// <returns>File content as a string, or null if the file doesn't exist or exceeds the size limit.</returns>
    public static string? ReadAllTextSafe(string path, long maxBytes = DefaultMaxTextFileSize)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > maxBytes)
            {
                AppLog.Warning(
                    "[SafeFile] Refusing to read '{Path}': size {Size} bytes exceeds limit {Limit} bytes",
                    path, fileInfo.Length, maxBytes);
                LastError = $"File too large: {fileInfo.Length} > {maxBytes}";
                return null;
            }

            LastError = null;
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[SafeFile] Failed to read '{Path}'", path);
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Write text to a file, creating parent directories as needed.
    /// Silently catches all exceptions (best-effort write for config files).
    /// </summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <param name="content">Text content to write.</param>
    public static void WriteAllTextSafe(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            LastError = null;
            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[SafeFile] Failed to write '{Path}'", path);
            LastError = ex.Message;
        }
    }
}
