// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;

namespace Vonvert.Engine;

/// <summary>
/// Single source of truth for where the app keeps its user data (presets, config,
/// hotkeys, layout, logs). Everything is derived from <see cref="Root"/>.
///
/// The root is normally <see cref="DefaultRoot"/> but can be redirected by the
/// user from Settings. Because the redirect itself must be discoverable before any
/// consumer runs, the chosen location is recorded in a small fixed "pointer" file
/// that always lives under <see cref="DefaultRoot"/> (which never moves). At the
/// earliest startup tick we read that pointer; if it names a usable directory we
/// honour it, otherwise we fall back to the default.
///
/// This build's default root is publisher-namespaced (%APPDATA%\ninesix-ai\Vonvert)
/// so it never collides with other Vonvert editions that use the bare %APPDATA%\Vonvert.
/// </summary>
public static class AppPaths
{
    // ── Test seams (internal; the app never sets these) ──────────────
    internal static string? RootOverride;          // forces Root when non-null
    internal static string? PointerDirOverride;    // relocates the pointer file for tests
    internal static string[]? PortableArgsOverride;   // forces the command-line for tests
    internal static string? PortableExeDirOverride;   // forces the exe directory for tests

    private const string PortableFlag = "--portable";
    private const string PortableMarkerFileName = "vonvert.portable";
    private const string PortableDataSubdir = "data";
    private const string InstalledMutexName = "Vonvert_SingleInstance_Mutex";

    /// <summary>Fixed per-build default; also the home of the pointer file.</summary>
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ninesix-ai", "Vonvert");

    /// <summary>Absolute path of the pointer file (lives under the immovable default root).</summary>
    public static string PointerFilePath => Path.Combine(
        PointerDirOverride ?? DefaultRoot, "data_location.txt");

    private static string? _cached;
    private static bool? _isPortable;

    /// <summary>The active data root. Resolved once, then cached.</summary>
    public static string Root
    {
        get
        {
            if (RootOverride != null) return RootOverride;
            return _cached ??= Resolve();
        }
    }

    /// <summary>True only when a portable trigger is present AND <c>exe\data</c> is usable.</summary>
    public static bool IsPortable
    {
        get
        {
            _ = Root;                       // ensure Resolve() has run and populated _isPortable
            return _isPortable == true;
        }
    }

    /// <summary>Pure: does this launch request portable mode (flag or marker file)? Best-effort on IO.</summary>
    public static bool IsPortableRequested(string[]? args, string? exeDir)
    {
        if (args != null)
            foreach (var a in args)
                if (string.Equals(a, PortableFlag, StringComparison.OrdinalIgnoreCase))
                    return true;
        try
        {
            if (!string.IsNullOrEmpty(exeDir) &&
                File.Exists(Path.Combine(exeDir, PortableMarkerFileName)))
                return true;
        }
        catch { /* detection is best-effort */ }
        return false;
    }

    /// <summary>Create and return <c>exe\data</c>, or null when it cannot be made writable
    /// (e.g. the app sits in a read-only location). Never logs — may run before AppLog exists.</summary>
    private static string? TryResolvePortableRoot(string exeDir)
    {
        try
        {
            var data = Path.Combine(exeDir, PortableDataSubdir);
            Directory.CreateDirectory(data);          // idempotent; throws when read-only
            return IsUsablePath(data) ? data : null;
        }
        catch { return null; }
    }

    /// <summary>Single-instance lock name: portable instances get a per-exe-dir name so a
    /// portable build and the installed build (and different portable dirs) can run at once.</summary>
    public static string MutexNameFor(bool portable, string exeDir)
    {
        if (!portable) return InstalledMutexName;
        string key;
        try { key = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exeDir)); }
        catch { key = exeDir ?? string.Empty; }
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key.ToUpperInvariant()));
        return "Vonvert_Portable_" + Convert.ToHexString(hash.AsSpan(0, 8));
    }

    private static string Resolve()
    {
        // Portable first: honour the trigger, and if exe\data is writable make it the single
        // data root, IGNORING the pointer redirect (user explicitly asked to follow the exe).
        string exeDir = PortableExeDirOverride ?? AppDomain.CurrentDomain.BaseDirectory;
        string[] args = PortableArgsOverride ?? Environment.GetCommandLineArgs();
        _isPortable = false;
        if (IsPortableRequested(args, exeDir))
        {
            var proot = TryResolvePortableRoot(exeDir);
            if (proot != null) { _isPortable = true; return proot; }
        }

        try
        {
            if (File.Exists(PointerFilePath))
            {
                var content = File.ReadAllText(PointerFilePath);
                var resolved = ResolveFromPointer(content, DefaultRoot);
                if (resolved != DefaultRoot) return resolved;
            }
        }
        catch
        {
            // Resolve() can run before AppLog is initialised (AppLog itself derives its
            // folder from Root), so never log here — silently fall back to the default.
        }
        return DefaultRoot;
    }

    /// <summary>
    /// Pure decision used by <see cref="Resolve"/> and unit tests: a non-empty,
    /// rooted, character-valid path wins; anything else falls back to <paramref name="defaultRoot"/>.
    /// </summary>
    public static string ResolveFromPointer(string? pointerContent, string defaultRoot)
    {
        var candidate = pointerContent?.Trim();
        if (string.IsNullOrEmpty(candidate)) return defaultRoot;
        return IsUsablePath(candidate) ? candidate : defaultRoot;
    }

    /// <summary>True when <paramref name="path"/> is an absolute path with no illegal characters.</summary>
    public static bool IsUsablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!Path.IsPathRooted(path)) return false;
        var invalid = Path.GetInvalidPathChars();
        foreach (var c in path)
        {
            if (Array.IndexOf(invalid, c) >= 0) return false;
            // Wildcards are legal in GetInvalidPathChars' set but can never appear in a
            // real, creatable directory path — reject them too.
            if (c == '?' || c == '*') return false;
        }
        return true;
    }

    /// <summary>
    /// Record a new data root by writing the pointer file under the (immovable)
    /// default root. Returns false if the path is unusable or the write fails.
    /// Takes effect after an application restart.
    /// </summary>
    public static bool TrySetRoot(string newRoot)
    {
        if (!IsUsablePath(newRoot)) return false;
        try
        {
            var dir = Path.GetDirectoryName(PointerFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(PointerFilePath, newRoot);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[AppPaths] Failed to persist data-location pointer.");
            return false;
        }
    }

    /// <summary>Clear the cached root so the next <see cref="Root"/> read re-resolves (used after a change).</summary>
    public static void Invalidate() { _cached = null; _isPortable = null; }
}
