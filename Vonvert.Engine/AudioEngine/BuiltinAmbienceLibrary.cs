// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Reflection;

namespace Vonvert.Engine.AudioEngine;

/// <summary>One built-in seamless-loop ambience clip available to the BGM feature.</summary>
public sealed record BuiltinAmbienceSound(
    string Id,
    string FileName,
    string DisplayName,
    string Source);

/// <summary>
/// Catalog of the built-in seamless-loop ambience clips shipped with the app for
/// the BGM background-music feature, plus lazy extraction of the embedded WAV
/// resources to disk.
/// </summary>
/// <remarks>
/// Every clip is <b>original material procedurally synthesized in-repo</b> (see
/// <c>tools/synth_ambience.py</c>) at 48 kHz / mono / 16-bit PCM with a seamless
/// cross-faded loop. There is no third-party audio, so nothing here needs to be
/// listed in a third-party notices file, and the clips are Apache-2.0 clean.
///
/// Clips are embedded as managed resources in Vonvert.Engine and lazily extracted
/// to <c>%APPDATA%\Vonvert\BuiltinAmbience</c> on first use, then handed to
/// <see cref="BackgroundMusicPlayer.Load"/> so the existing transport, ducking and
/// rendering are reused unchanged. Localized display names live in the UI layer's
/// translation tables; this catalog carries the English label only.
/// </remarks>
public static class BuiltinAmbienceLibrary
{
    /// <summary>LogicalName prefix declared in Vonvert.Engine.csproj.</summary>
    public const string EmbeddedPrefix = "Vonvert.Engine.AudioEngine.BuiltinAmbience.";

    /// <summary>Source note shared by all clips (original synthesis, no third party).</summary>
    public const string OriginalSynthesisSource =
        "Programmatically synthesized in-repo under Apache-2.0 (original, no third-party material)";

    /// <summary>Default cache folder used when no override is supplied.</summary>
    public static readonly string DefaultCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Vonvert", "BuiltinAmbience");

    /// <summary>The ten original ambience clips shipped with the app.</summary>
    public static IReadOnlyList<BuiltinAmbienceSound> All { get; } = new[]
    {
        new BuiltinAmbienceSound("rain",         "rain.wav",        "Rain",             OriginalSynthesisSource),
        new BuiltinAmbienceSound("white-noise",  "white.wav",       "White Noise",      OriginalSynthesisSource),
        new BuiltinAmbienceSound("cafe",         "cafe.wav",        "Coffee Shop",      OriginalSynthesisSource),
        new BuiltinAmbienceSound("forest",       "forest.wav",      "Forest Birds",     OriginalSynthesisSource),
        new BuiltinAmbienceSound("ocean",        "ocean.wav",       "Ocean Waves",      OriginalSynthesisSource),
        new BuiltinAmbienceSound("radio-static", "static.wav",      "Radio Static",     OriginalSynthesisSource),
        new BuiltinAmbienceSound("fan",          "fan.wav",         "Fan",              OriginalSynthesisSource),
        new BuiltinAmbienceSound("keyboard",     "keyboard.wav",    "Keyboard Typing",  OriginalSynthesisSource),
        new BuiltinAmbienceSound("campfire",     "campfire.wav",    "Campfire",         OriginalSynthesisSource),
        new BuiltinAmbienceSound("livestream",   "livestream.wav",  "Stream Room Floor", OriginalSynthesisSource),
    };

    /// <summary>Raw bytes of an embedded ambience WAV, or null when missing.</summary>
    internal static byte[]? GetBuiltinBytes(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedPrefix + fileName);
        if (stream == null)
        {
            AppLog.Warning("[BuiltinAmbience] Embedded resource '{Name}' not found.", fileName);
            return null;
        }
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Extract a built-in clip into the cache folder (idempotent: skipped when a
    /// same-size file already exists) and return its absolute path, or null on failure.
    /// </summary>
    public static string? ResolveToCache(BuiltinAmbienceSound sound, string? cacheDir = null)
    {
        var bytes = GetBuiltinBytes(sound.FileName);
        if (bytes == null || bytes.Length == 0)
            return null;

        var dir = cacheDir ?? DefaultCacheDir;
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, sound.FileName);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
                File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[BuiltinAmbience] Failed to cache clip '{Id}'.", sound.Id);
            return null;
        }
    }
}
