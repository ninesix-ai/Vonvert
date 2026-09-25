// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Executes the uninstall data purge by delegating WHAT to delete to the pure
// PurgeTargetResolver and then performing the deletions. Idempotent (missing paths
// are skipped) and fault-tolerant (a single failing item is recorded and the rest
// still proceed). Recordings are never targeted. Registry access is abstracted so
// tests can inject a fake hive and never touch the real HKCU.

namespace Vonvert.Engine;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

public sealed class PurgeOptions
{
    public required string DefaultRoot { get; init; }
    public string? PointerFilePath { get; init; }
    public string? LegacyBareRoot { get; init; }
    public string? CrashDumpPath { get; init; }
    public string RegistrySubKey { get; init; } = @"Software\Vonvert";

    /// <summary>Assembles production options from the real environment / AppPaths.</summary>
    public static PurgeOptions FromEnvironment()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new PurgeOptions
        {
            DefaultRoot = AppPaths.DefaultRoot,
            PointerFilePath = AppPaths.PointerFilePath,
            LegacyBareRoot = Path.Combine(appData, "Vonvert"),
            CrashDumpPath = Path.Combine(Path.GetTempPath(), "Vonvert_crash.txt"),
            RegistrySubKey = @"Software\Vonvert",
        };
    }
}

public sealed class PurgeResult
{
    public List<string> DeletedPaths { get; } = new();
    public List<string> SkippedPaths { get; } = new();
    public List<string> FailedPaths { get; } = new();
    public int ExitCode { get; set; }
}

public interface IRegistryCleaner
{
    /// <summary>Delete an HKCU-relative sub-key tree. Returns true if a key was removed.</summary>
    bool DeleteSubKeyTree(string relativeHkcuKey);
}

public sealed class WindowsRegistryCleaner : IRegistryCleaner
{
    public bool DeleteSubKeyTree(string relativeHkcuKey)
    {
        using var cu = Registry.CurrentUser;
        if (cu.OpenSubKey(relativeHkcuKey) == null) return false;
        cu.DeleteSubKeyTree(relativeHkcuKey, throwOnMissingSubKey: false);
        return true;
    }
}

/// <summary>
/// Deletes all Vonvert user data (config/presets/logs including a pointer-redirected
/// root, the legacy bare %APPDATA%\Vonvert, the HKCU settings key and the crash dump).
/// </summary>
public sealed class UserDataPurger
{
    private readonly IRegistryCleaner _registry;

    public UserDataPurger(IRegistryCleaner? registry = null) =>
        _registry = registry ?? new WindowsRegistryCleaner();

    public PurgeResult Purge(PurgeOptions options)
    {
        var result = new PurgeResult();

        string? pointerContent = null;
        try
        {
            if (options.PointerFilePath != null && File.Exists(options.PointerFilePath))
                pointerContent = File.ReadAllText(options.PointerFilePath);
        }
        catch
        {
            pointerContent = null;   // unreadable pointer -> behave as no redirect
        }

        var targets = PurgeTargetResolver.Resolve(
            pointerContent, options.DefaultRoot, options.LegacyBareRoot, options.CrashDumpPath);

        foreach (var dir in targets.Directories)
        {
            try
            {
                if (Directory.Exists(dir)) { Directory.Delete(dir, true); result.DeletedPaths.Add(dir); }
                else result.SkippedPaths.Add(dir);
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, $"[Purge] Failed to delete directory {dir}");
                result.FailedPaths.Add(dir);
                result.ExitCode = 1;
            }
        }

        foreach (var file in targets.Files)
        {
            try
            {
                if (File.Exists(file)) { File.Delete(file); result.DeletedPaths.Add(file); }
                else result.SkippedPaths.Add(file);
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, $"[Purge] Failed to delete file {file}");
                result.FailedPaths.Add(file);
                result.ExitCode = 1;
            }
        }

        if (targets.RegistrySubKey != null)
        {
            var key = "HKCU\\" + targets.RegistrySubKey;
            try
            {
                if (_registry.DeleteSubKeyTree(targets.RegistrySubKey)) result.DeletedPaths.Add(key);
                else result.SkippedPaths.Add(key);
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, $"[Purge] Failed to delete registry {targets.RegistrySubKey}");
                result.FailedPaths.Add(key);
                result.ExitCode = 1;
            }
        }

        return result;
    }
}
