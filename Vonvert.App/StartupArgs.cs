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

    public static bool IsPurgeMode(string[]? args) =>
        args != null && System.Array.IndexOf(args, PurgeFlag) >= 0;
}
