// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Built-in preset name → gallery category mapping.
/// Registered by PresetManager so all presets carry category metadata in the PresetIndex.
/// </summary>
public static class PresetCategoryMap
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["Normal"]    = "Voice",
        ["Deep Male"] = "Voice",
        ["Female"]    = "Voice",
        ["Robot"]     = "FX",
        ["Demon"]     = "FX",
    };

    /// <summary>Look up the gallery category for a built-in preset name.</summary>
    public static bool TryGet(string presetName, out string category) => Map.TryGetValue(presetName, out category!);
}
