// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

/// <summary>
/// Pure, WPF-free decision of whether the launch arguments request a data purge.
/// Kept separate from <c>App</c> so it can be unit-tested without starting the UI.
/// </summary>
public static class StartupArgs
{
    public const string PurgeFlag = "--purge-user-data";
    public const string PurgeRootFlag = "--purge-root";

    public static bool IsPurgeMode(string[]? args) =>
        args != null && System.Array.IndexOf(args, PurgeFlag) >= 0;

    /// <summary>Value following <see cref="PurgeRootFlag"/>, or null when the flag is
    /// absent or its value is missing/blank/looks like another flag (caller then does
    /// a full purge). Taken verbatim; path validation is the purger's concern.</summary>
    public static string? GetPurgeRoot(string[]? args)
    {
        if (args == null) return null;
        int i = System.Array.IndexOf(args, PurgeRootFlag);
        if (i < 0 || i + 1 >= args.Length) return null;
        string value = args[i + 1];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-")) return null;
        return value;
    }
}
