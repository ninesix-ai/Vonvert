// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.PresetLibrary;

public sealed class PresetManager
{
    private readonly string _folder;
    private readonly List<VoiceProfile> _presets = [];

    public IReadOnlyList<VoiceProfile> Presets => _presets;
    public PresetIndex Index { get; }
    public event Action? PresetsChanged;

    public PresetManager(string presetFolder)
    {
        _folder = presetFolder;
        Directory.CreateDirectory(_folder);
        var indexPath = Path.Combine(_folder, "_index.json");
        Index = new PresetIndex(indexPath);
        LoadBuiltIns();
        LoadFromDisk();
    }

    /// <summary>Save a preset to disk.</summary>
    public bool Save(VoiceProfile preset)
    {
        string path = UniquePresetPath(preset.Name);
        SafeFileHelper.WriteAllTextSafe(path, JsonConvert.SerializeObject(preset, Formatting.Indented));
        if (!_presets.Any(p => p.Name == preset.Name))
        {
            _presets.Add(preset);
            Index.AddEntry(preset.Name);
        }
        PresetsChanged?.Invoke();
        return true;
    }

    public void Delete(VoiceProfile preset)
    {
        // Deletion must remove EVERY file that declares this preset's name.
        // The unique-path guard in Save can leave invisible shadow files
        // ("Name_1.json") on disk; deleting only "Name.json" lets the preset
        // resurrect on the next LoadFromDisk.
        if (!string.IsNullOrEmpty(preset.Name))
        {
            foreach (var file in Directory.GetFiles(_folder, "*.json"))
            {
                if (string.Equals(Path.GetFileName(file), "_index.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    var json = SafeFileHelper.ReadAllTextSafe(file);
                    if (json == null) continue;
                    var root = JObject.Parse(json);
                    var name = root.GetValue("Name", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    if (string.Equals(name, preset.Name, StringComparison.Ordinal))
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    AppLog.Warning(ex, "[PresetManager] Delete: failed to inspect '{File}'", file);
                }
            }
        }
        _presets.Remove(preset);
        Index.RemoveEntry(preset.Name);
        PresetsChanged?.Invoke();
    }

    private void LoadFromDisk()
    {
        var builtInNames = _presets.Select(p => p.Name).ToHashSet();
        // Use safe deserialization settings
        var safeSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

        foreach (var file in Directory.GetFiles(_folder, "*.json"))
        {
            // Skip the index file: it is metadata, not a preset. Deserializing it
            // as a VoiceProfile would yield a phantom entry with the default name.
            if (string.Equals(Path.GetFileName(file), "_index.json", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var json = SafeFileHelper.ReadAllTextSafe(file);
                if (json == null) continue;
                var p = JsonConvert.DeserializeObject<VoiceProfile>(json, safeSettings);
                if (p != null)
                {
                    // Disk files are as untrusted as imports (hand-edited,
                    // or written by an earlier unguarded import).
                    PresetValueGuard.Sanitize(p);

                    if (builtInNames.Contains(p.Name))
                    {
                        var hardcoded = _presets.First(x => x.Name == p.Name);
                        SafeFileHelper.WriteAllTextSafe(file, JsonConvert.SerializeObject(hardcoded, Formatting.Indented));
                    }
                    // Skip any known built-in name not loaded from the manifest
                    else if (AllBuiltInPresetNames.Contains(p.Name))
                    {
                        // Built-in preset not available — skip
                    }
                    else if (!_presets.Any(x => x.Name == p.Name))
                    {
                        _presets.Add(p);
                        Index.AddEntry(p.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "[PresetManager] Failed to load user preset from '{File}'", file);
            }
        }

        // Ensure all loaded built-ins exist on disk and in index
        foreach (var builtIn in _presets.Where(p => builtInNames.Contains(p.Name)))
        {
            string path = Path.Combine(_folder, $"{SanitizeName(builtIn.Name)}.json");
            if (!File.Exists(path))
                SafeFileHelper.WriteAllTextSafe(path, JsonConvert.SerializeObject(builtIn, Formatting.Indented));
        }
    }

    private void LoadBuiltIns()
    {
        // Dynamically discover and load embedded preset packs from the manifest.
        // Adding a new pack: declare it in presets-manifest.json with a resourceName.
        foreach (var pack in BuiltInPresets.Packs)
        {
            if (string.IsNullOrEmpty(pack.ResourceName)) continue;
            var presets = LoadEmbeddedPresets(pack.ResourceName);
            _presets.AddRange(presets);
        }

        // Assign categories and register in index
        foreach (var preset in _presets)
        {
            Index.AddEntry(preset.Name);
            if (PresetCategoryMap.TryGet(preset.Name, out var cat))
            {
                Index.UpdateMetadata(preset.Name, m => m.Category = cat);
            }
        }
    }

    /// <summary>
    /// Load a list of VoiceProfile presets from an embedded JSON resource.
    /// </summary>
    private static List<VoiceProfile> LoadEmbeddedPresets(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            AppLog.Warning("[PresetManager] Embedded resource '{Name}' not found.", resourceName);
            return new();
        }
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        try
        {
            return JsonConvert.DeserializeObject<List<VoiceProfile>>(json) ?? new();
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[PresetManager] Failed to parse embedded preset JSON '{Name}'.", resourceName);
            return new();
        }
    }

    // ── Built-in preset names (used to distinguish custom from built-in) ──

    /// <summary>All known built-in preset names, used to distinguish
    /// custom presets from built-in ones.
    /// Delegated to <see cref="BuiltInPresets.AllNames"/> — the single source of truth.</summary>
    private static IReadOnlySet<string> AllBuiltInPresetNames => BuiltInPresets.AllNames;

    private HashSet<string> GetBuiltInNames() => _presets
        .Select(p => p.Name)
        .Where(n => AllBuiltInPresetNames.Contains(n))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Export a preset to an external .vopreset file.
    /// The file contains the VoiceProfile plus its PresetMetadata (name, notes,
    /// tags, ...) so the exported file round-trips completely.</summary>
    public void Export(VoiceProfile preset, string outputPath)
    {
        var data = new PresetFileData
        {
            Format = "vonvert-preset",
            Version = 1,
            Preset = preset,
            Metadata = Index.GetMetadata(preset.Name)
        };
        // camelCase envelope keys ({format, version, preset, metadata}) for a
        // tidy single-file format; Import matches keys case-insensitively.
        var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        });
        SafeFileHelper.WriteAllTextSafe(outputPath, json);
    }

    /// <summary>Import a preset from an external .vopreset file and add it to the library.
    /// Legacy .vmpreset files (plain VoiceProfile JSON) remain importable.
    /// Returns null when the file is invalid.
    /// Uses explicit TypeNameHandling.None to prevent type confusion attacks.</summary>
    public VoiceProfile? Import(string filePath)
    {
        try
        {
            var json = SafeFileHelper.ReadAllTextSafe(filePath);
            if (json == null) return null;
            // Explicitly disable type name handling to prevent deserialization attacks
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

            var root = JsonConvert.DeserializeObject<JObject>(json, settings);
            if (root == null) return null;

            VoiceProfile? preset;
            PresetMetadata? metadata = null;
            if (root.TryGetValue("preset", StringComparison.OrdinalIgnoreCase, out var presetToken))
            {
                // New .vopreset envelope: { format, version, preset, metadata }
                // (keys are matched case-insensitively; Newtonsoft serializes
                // PascalCase property names by default)
                preset = presetToken.ToObject<VoiceProfile>(JsonSerializer.Create(settings));
                if (root.TryGetValue("metadata", StringComparison.OrdinalIgnoreCase, out var metaToken))
                    metadata = metaToken.ToObject<PresetMetadata>(JsonSerializer.Create(settings));
            }
            else
            {
                // Legacy .vmpreset: plain VoiceProfile JSON
                preset = root.ToObject<VoiceProfile>(JsonSerializer.Create(settings));
            }
            if (preset == null) return null;

            // Sanitize NaN/Infinity and absurd magnitudes before anything
            // is persisted — one poisoned parameter silently ruins every output frame.
            PresetValueGuard.Sanitize(preset);

            // Avoid name collisions: append " (imported)" if name already exists
            if (_presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
                preset.Name = preset.Name + " (imported)";

            if (!Save(preset)) return null;

            // Restore metadata carried by the .vopreset envelope (if present)
            if (metadata != null)
            {
                Index.UpdateMetadata(preset.Name, m =>
                {
                    if (!string.IsNullOrEmpty(metadata.Notes)) m.Notes = metadata.Notes;
                    if (metadata.Tags != null && metadata.Tags.Count > 0) m.Tags = new List<string>(metadata.Tags);
                    if (!string.IsNullOrEmpty(metadata.Category)) m.Category = metadata.Category;
                    m.IsFavorite = metadata.IsFavorite;
                    if (metadata.LastUsed > DateTime.MinValue) m.LastUsed = metadata.LastUsed;
                    m.UseCount = metadata.UseCount;
                    if (metadata.Created > DateTime.MinValue) m.Created = metadata.Created;
                });
            }

            return preset;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Serialized envelope for the single-file preset format (.vopreset).</summary>
    private sealed class PresetFileData
    {
        public string Format { get; set; } = "vonvert-preset";
        public int Version { get; set; } = 1;
        public VoiceProfile Preset { get; set; } = new();
        public PresetMetadata? Metadata { get; set; }
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Generate a unique file path for a preset, appending a numeric
    /// suffix if a file with the same sanitized name already exists on disk.
    /// This prevents accidental overwrites when two presets share a name.</summary>
    private string UniquePresetPath(string name)
    {
        var sanitized = SanitizeName(name);
        var basePath = Path.Combine(_folder, $"{sanitized}.json");
        if (!File.Exists(basePath)) return basePath;

        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(_folder, $"{sanitized}_{i}.json");
            if (!File.Exists(candidate)) return candidate;
        }
        // Extremely unlikely: 1000 variants all taken — fall back to GUID
        return Path.Combine(_folder, $"{sanitized}_{Guid.NewGuid():N}.json");
    }
}
