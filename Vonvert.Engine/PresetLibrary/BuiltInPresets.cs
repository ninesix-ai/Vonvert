// SPDX-License-Identifier: MIT
// Copyright © ninesix-ai studio

using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Single source of truth for all built-in preset names.
/// Reads pack declarations from the embedded <c>presets-manifest.json</c> resource
/// at first access. Adding a new preset pack now requires only:
/// <list type="number">
///   <item>Add an entry to <c>presets-manifest.json</c></item>
///   <item>Place the preset JSON resource file (if the pack has embedded VoiceProfile data)</item>
/// </list>
/// No C# code change is required.
/// </summary>
public static class BuiltInPresets
{
    private const string ManifestResourceName =
        "Vonvert.Engine.PresetLibrary.BuiltIn.presets-manifest.json";

    // ── Parsed pack descriptors ──────────────────────────────────────

    /// <summary>Describes a single preset pack declared in the manifest.</summary>
    public sealed class PackDescriptor
    {
        public string Id { get; init; } = "";
        public string? ResourceName { get; init; }
        public string Description { get; init; } = "";
        public IReadOnlyList<string> PresetNames { get; init; } = [];
    }

    /// <summary>All packs declared in the manifest, in declaration order.</summary>
    public static IReadOnlyList<PackDescriptor> Packs { get; } = LoadPacks();

    // ── Derived sets (computed once from manifest) ───────────────────

    /// <summary>All built-in preset names across every pack.</summary>
    public static IReadOnlySet<string> AllNames { get; } = BuildAll();

    // ── Internal helpers ─────────────────────────────────────────────

    private static List<PackDescriptor> LoadPacks()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ManifestResourceName);
        if (stream == null)
        {
            AppLog.Warning("[BuiltInPresets] Manifest resource '{Resource}' not found. " +
                           "Falling back to empty pack list.", ManifestResourceName);
            return [];
        }

        try
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var root = JObject.Parse(json);
            var packsArray = root["packs"] as JArray
                ?? throw new InvalidOperationException("Manifest missing 'packs' array");

            var packs = new List<PackDescriptor>();
            foreach (var packToken in packsArray)
            {
                var packObj = packToken as JObject
                    ?? throw new InvalidOperationException("Invalid pack entry in manifest");

                var names = (packObj["presetNames"] as JArray)?
                    .Select(t => t.Value<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? [];

                packs.Add(new PackDescriptor
                {
                    Id = packObj["id"]?.Value<string>() ?? "",
                    ResourceName = packObj["resourceName"]?.Value<string>(),
                    Description = packObj["description"]?.Value<string>() ?? "",
                    PresetNames = names,
                });
            }

            AppLog.Information("[BuiltInPresets] Loaded {Count} packs ({TotalNames} preset names) from manifest.",
                packs.Count, packs.Sum(p => p.PresetNames.Count));
            return packs;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "[BuiltInPresets] Failed to parse manifest '{Resource}'.", ManifestResourceName);
            return [];
        }
    }

    private static HashSet<string> BuildAll()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in Packs)
            foreach (var name in pack.PresetNames)
                set.Add(name);
        return set;
    }
}
