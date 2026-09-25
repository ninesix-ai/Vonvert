// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Pure decision layer for the uninstall data purge: turns a pointer file's raw
// content plus the known fixed roots into the set of directories/files/registry key
// to delete. Performs NO file-system or registry access, so it is fully unit-testable.
// Recordings (a user-chosen directory) are never passed in and therefore never appear.

namespace Vonvert.Engine;

using System;
using System.Collections.Generic;

public sealed record PurgeTargets(
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> Files,
    string? RegistrySubKey);

public static class PurgeTargetResolver
{
    public static PurgeTargets Resolve(
        string? pointerContent,
        string defaultRoot,
        string? legacyBareRoot,
        string? crashDumpPath)
    {
        var dirs = new List<string>();

        // A redirected (custom) root only wins when the pointer resolves away from default.
        var custom = AppPaths.ResolveFromPointer(pointerContent, defaultRoot);
        if (custom != defaultRoot)
            AddDistinct(dirs, custom);

        AddDistinct(dirs, defaultRoot);

        if (!string.IsNullOrWhiteSpace(legacyBareRoot))
            AddDistinct(dirs, legacyBareRoot!);

        var files = new List<string>();
        if (!string.IsNullOrWhiteSpace(crashDumpPath))
            files.Add(crashDumpPath!);

        return new PurgeTargets(dirs, files, @"Software\Vonvert");
    }

    private static void AddDistinct(List<string> list, string value)
    {
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }
}
