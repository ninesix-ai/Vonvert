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

    /// <summary>Fixed per-build default; also the home of the pointer file.</summary>
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ninesix-ai", "Vonvert");

    /// <summary>Absolute path of the pointer file (lives under the immovable default root).</summary>
    public static string PointerFilePath => Path.Combine(
        PointerDirOverride ?? DefaultRoot, "data_location.txt");

    private static string? _cached;

    /// <summary>The active data root. Resolved once, then cached.</summary>
    public static string Root
    {
        get
        {
            if (RootOverride != null) return RootOverride;
            return _cached ??= Resolve();
        }
    }

    private static string Resolve()
    {
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
    public static void Invalidate() => _cached = null;
}
