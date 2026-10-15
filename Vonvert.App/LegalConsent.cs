// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.IO;

namespace Vonvert.App;

/// <summary>
/// Records the user's one-time acceptance of the legal consent terms and decides
/// whether the first-run dialog must appear. Pure logic over an injected root path
/// (the marker lives under AppPaths.Root, so it follows portable/relocated data).
/// Bump <see cref="ConsentVersion"/> when the displayed terms materially change to
/// re-prompt existing users.
/// </summary>
public static class LegalConsent
{
    /// <summary>Version of the consent text the app currently requires.</summary>
    public const int ConsentVersion = 1;

    private const string MarkerFileName = "legal_consent.txt";

    public static string MarkerPath(string root) => Path.Combine(root, MarkerFileName);

    /// <summary>True when a marker recording acceptance at >= currentVersion exists.</summary>
    public static bool IsAccepted(string root, int currentVersion)
    {
        try
        {
            if (!File.Exists(MarkerPath(root))) return false;
            var raw = File.ReadAllText(MarkerPath(root)).Trim();
            return int.TryParse(raw, out var v) && v >= currentVersion;
        }
        catch { return false; }
    }

    /// <summary>Persist acceptance at currentVersion. Returns false on IO failure.</summary>
    public static bool Record(string root, int currentVersion)
    {
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(MarkerPath(root), currentVersion.ToString());
            return true;
        }
        catch { return false; }
    }
}
