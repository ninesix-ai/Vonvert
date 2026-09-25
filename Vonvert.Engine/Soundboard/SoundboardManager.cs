// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Concurrent;
using System.Text.Json;
using NAudio.Wave;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.ProceduralAudio;

namespace Vonvert.Engine.Soundboard;

/// <summary>
/// Manages the soundboard catalogue: built-in procedurally generated sounds plus
/// user-imported audio files. Caches PCM data and plays it through the VoiceEngine
/// one-shot mixer. Supports per-sound hotkey bindings with JSON persistence.
/// </summary>
public sealed class SoundboardManager
{
    private readonly List<SoundDefinition> _sounds;
    private readonly object _soundsLock = new();
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly string _userFolder;

    // Hotkey bindings: soundId -> SoundHotkeyBinding
    private readonly Dictionary<string, SoundHotkeyBinding> _hotkeyBindings = new();

    /// <summary>All available sounds (built-in + user-imported). Returns a thread-safe snapshot.</summary>
    public IReadOnlyList<SoundDefinition> Sounds { get { lock (_soundsLock) { return _sounds.ToList(); } } }

    /// <summary>All category names in display order.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>Master volume for soundboard playback (0.0 .. 1.0).</summary>
    public float Volume { get; set; } = 0.35f;

    /// <summary>Local-only audition channel. Always played on <see cref="Play"/>;
    /// wired by the App layer. Null degrades to broadcast-only behaviour.</summary>
    public IAuditionPlayer? Audition { get; set; }

    /// <summary>When true, <see cref="Play"/> also broadcasts through the engine so
    /// others hear it; when false (default) pads are heard only locally.</summary>
    public bool LiveMode { get; set; }

    /// <summary>Current hotkey bindings (read-only view).</summary>
    public IReadOnlyDictionary<string, SoundHotkeyBinding> HotkeyBindings => _hotkeyBindings;

    /// <summary>Raised when the sound list changes (import / delete).</summary>
    public event Action? SoundsChanged;

    /// <summary>Raised when hotkey bindings change.</summary>
    public event Action? HotkeysChanged;

    /// <summary>
    /// Creates a new SoundboardManager and loads the built-in procedural catalogue
    /// plus any user-imported sounds and hotkey bindings from the data folder.
    /// </summary>
    public SoundboardManager()
    {
        _sounds = new List<SoundDefinition>(SoundGenerator.GetAllSounds());
        Categories = SoundGenerator.GetCategories();

        _userFolder = Path.Combine(AppPaths.Root, "Soundboard");

        LoadUserSounds();
        LoadHotkeyConfig();
        AppLog.Information("[Soundboard] Initialized. {Count} sounds, {Hotkeys} hotkey bindings, folder: {Folder}",
            _sounds.Count, _hotkeyBindings.Count, _userFolder);
    }

    /// <summary>Number of user-imported sound slots currently in use.</summary>
    public int UserSlotCount { get { lock (_soundsLock) { return _sounds.Count(s => s.SourceType == SoundSourceType.File); } } }

    /// <summary>Returns the index of a sound by its ID, or -1 if not found.</summary>
    public int IndexOfSound(string soundId)
    {
        lock (_soundsLock) { return _sounds.FindIndex(s => s.Id == soundId); }
    }

    // ════ Playback ════

    /// <summary>
    /// Plays a sound by ID through the engine's soundboard mixer. For generated sounds
    /// the data is produced on first call then cached; for file-based sounds the file is
    /// read and resampled to float32 mono @ EngineRate.
    /// </summary>
    public void Play(string soundId, VoiceEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        try
        {
            var data = GetSoundData(soundId);
            if (data.Length == 0)
            {
                AppLog.Warning("[Soundboard] Play requested but no data for: {Id}", soundId);
                return;
            }

            // Local audition always (device-independent, works with engine stopped).
            Audition?.Play(data, Volume);

            // Broadcast to others only in live mode.
            if (LiveMode)
                engine.PlaySoundboardBytes(data, Volume);

            AppLog.Debug("[Soundboard] Played: {Id} (live={Live}, {Bytes} bytes)", soundId, LiveMode, data.Length);
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[Soundboard] Play failed for: {Id}", soundId);
        }
    }

    /// <summary>
    /// Returns the PCM byte data for a sound (cached after first generation).
    /// </summary>
    public byte[] GetSoundData(string soundId)
    {
        return _cache.GetOrAdd(soundId, id =>
        {
            SoundDefinition? def;
            lock (_soundsLock) { def = _sounds.FirstOrDefault(s => s.Id == id); }
            if (def == null) return Array.Empty<byte>();

            if (def.SourceType == SoundSourceType.Generated)
            {
                return SoundGenerator.GenerateBytes(id);
            }
            else
            {
                // File-based: read and resample to 48kHz mono float32
                return LoadAndResampleFile(id);
            }
        });
    }

    // ════ Import / Remove ════

    /// <summary>
    /// Imports an external audio file (MP3/WAV/OGG/M4A) into the soundboard.
    /// The file is copied to the user folder.
    /// </summary>
    public SoundDefinition ImportFile(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Audio file not found.", sourcePath);

        Directory.CreateDirectory(_userFolder);

        string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext != ".mp3" && ext != ".wav" && ext != ".ogg" && ext != ".m4a")
            throw new NotSupportedException($"Unsupported audio format: {ext}");

        string fileName = $"{Guid.NewGuid()}{ext}";
        string destPath = Path.Combine(_userFolder, fileName);
        File.Copy(sourcePath, destPath, overwrite: true);

        string displayName = Path.GetFileNameWithoutExtension(sourcePath);
        var def = new SoundDefinition(
            Id: $"user_{Guid.NewGuid():N}",
            Name: displayName,
            Category: "User",
            Emoji: "\U0001F4C1",
            SourceType: SoundSourceType.File
        );

        lock (_soundsLock) { _sounds.Add(def); }
        SoundsChanged?.Invoke();
        AppLog.Information("[Soundboard] Imported: {Name} ({Id}) from {Source}",
            displayName, def.Id, sourcePath);
        return def;
    }

    /// <summary>
    /// Removes a user-imported sound and deletes its file.
    /// </summary>
    public bool RemoveUserSound(string soundId)
    {
        SoundDefinition? def;
        lock (_soundsLock) { def = _sounds.FirstOrDefault(s => s.Id == soundId); }
        if (def == null || def.SourceType != SoundSourceType.File) return false;

        lock (_soundsLock) { _sounds.Remove(def); }
        _cache.TryRemove(soundId, out _);

        // Try to delete the file
        try
        {
            foreach (var f in Directory.GetFiles(_userFolder))
            {
                if (Path.GetFileNameWithoutExtension(f) == soundId)
                {
                    File.Delete(f);
                    break;
                }
            }
        }
        catch { /* best effort */ }

        SoundsChanged?.Invoke();
        AppLog.Information("[Soundboard] Removed: {Name} ({Id})", def.Name, soundId);
        return true;
    }

    // ════ Hotkey Bindings ════

    /// <summary>
    /// Assigns a hotkey binding to a sound.
    /// If another sound already uses this key combination, the old binding is removed.
    /// </summary>
    public void AssignHotkey(string soundId, SoundHotkeyBinding binding)
    {
        ArgumentNullException.ThrowIfNull(soundId);
        ArgumentNullException.ThrowIfNull(binding);

        // Remove any existing binding for the same key combination
        var conflicts = _hotkeyBindings.Where(kv =>
            kv.Value.VirtualKey == binding.VirtualKey &&
            kv.Value.Ctrl == binding.Ctrl &&
            kv.Value.Alt == binding.Alt &&
            kv.Value.Shift == binding.Shift).ToList();
        foreach (var c in conflicts)
            _hotkeyBindings.Remove(c.Key);

        _hotkeyBindings[soundId] = binding;
        SaveHotkeyConfig();
        HotkeysChanged?.Invoke();
    }

    /// <summary>Removes the hotkey binding for a sound.</summary>
    public bool RemoveHotkey(string soundId)
    {
        if (_hotkeyBindings.Remove(soundId))
        {
            SaveHotkeyConfig();
            HotkeysChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Gets the hotkey binding for a sound, or null if none assigned.</summary>
    public SoundHotkeyBinding? GetHotkey(string soundId)
    {
        return _hotkeyBindings.TryGetValue(soundId, out var binding) ? binding : null;
    }

    /// <summary>Finds the sound ID bound to a given key combination, or null.</summary>
    public string? FindSoundByHotkey(int virtualKey, bool ctrl, bool alt, bool shift)
    {
        foreach (var kv in _hotkeyBindings)
        {
            var b = kv.Value;
            if (b.VirtualKey == virtualKey && b.Ctrl == ctrl && b.Alt == alt && b.Shift == shift)
                return kv.Key;
        }
        return null;
    }

    // ════ Hotkey Persistence ════

    private static string HotkeyConfigPath => Path.Combine(AppPaths.Root, "soundboard-hotkeys.json");

    private void SaveHotkeyConfig()
    {
        var dict = new Dictionary<string, SoundHotkeyBinding>(_hotkeyBindings);
        SafeFileHelper.WriteAllTextSafe(HotkeyConfigPath,
            JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadHotkeyConfig()
    {
        var json = SafeFileHelper.ReadAllTextSafe(HotkeyConfigPath);
        if (json == null) return;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, SoundHotkeyBinding>>(json);
            if (dict == null) return;

            _hotkeyBindings.Clear();
            foreach (var kv in dict)
            {
                if (kv.Value != null)
                    _hotkeyBindings[kv.Key] = kv.Value;
            }
        }
        catch { /* hotkey config load is best-effort */ }
    }

    // ════ Private helpers ════

    private void LoadUserSounds()
    {
        if (!Directory.Exists(_userFolder)) return;

        try
        {
            foreach (var file in Directory.GetFiles(_userFolder))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".mp3" && ext != ".wav" && ext != ".ogg" && ext != ".m4a") continue;

                string id = $"user_{Path.GetFileNameWithoutExtension(file)}";
                string name = Path.GetFileNameWithoutExtension(file);
                var def = new SoundDefinition(id, name, "User", "\U0001F4C1", SoundSourceType.File);
                if (!_sounds.Any(s => s.Id == id))
                    lock (_soundsLock) { _sounds.Add(def); }
            }
        }
        catch { /* ignore errors during load */ }
    }

    private byte[] LoadAndResampleFile(string soundId)
    {
        SoundDefinition? def;
        lock (_soundsLock) { def = _sounds.FirstOrDefault(s => s.Id == soundId); }
        if (def == null) return Array.Empty<byte>();

        // Find the file in user folder
        string? filePath = null;
        if (Directory.Exists(_userFolder))
        {
            foreach (var f in Directory.GetFiles(_userFolder))
            {
                if ($"user_{Path.GetFileNameWithoutExtension(f)}" == soundId)
                {
                    filePath = f;
                    break;
                }
            }
        }

        if (filePath == null || !File.Exists(filePath)) return Array.Empty<byte>();

        try
        {
            WaveStream reader;
            if (filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                reader = new WaveFileReader(filePath);
            else
                reader = new AudioFileReader(filePath);

            using (reader)
            {
                var resampler = new MediaFoundationResampler(reader,
                    WaveFormat.CreateIeeeFloatWaveFormat(AudioConstants.EngineRate, 1));
                using (resampler)
                {
                    // Read up to 30 seconds
                    int maxBytes = AudioConstants.EngineRate * 4 * 30;
                    var buf = new byte[maxBytes];
                    int read = resampler.Read(buf, 0, buf.Length);
                    if (read < buf.Length)
                        Array.Resize(ref buf, read);
                    return buf;
                }
            }
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
