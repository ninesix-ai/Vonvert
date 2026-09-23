// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Newtonsoft.Json;

namespace Vonvert.Engine.PresetLibrary;

public enum StartupBehavior
{
    AutoApply,
    RestoreOnly,
    AskUser
}

public class PresetIndexSettings
{
    public int MaxRecentCount { get; set; } = 10;
    public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.AutoApply;
}

/// <threadsafety>
/// The debounced <see cref="Save"/> runs on a <see cref="System.Threading.Timer"/>
/// (thread-pool) thread while the mutating methods below are normally called from
/// the UI thread.  Every mutation, and the whole of <see cref="Save"/> (serialise
/// + disk write), is guarded by <c>lock (_sync)</c>: that serialises concurrent
/// Add/Remove against Save's JSON enumeration, guards in-place item edits (e.g.
/// UpdateMetadata mutating a Tags list) against serialization, and keeps two
/// overlapping Save calls from racing the same temp file.  The index is small and
/// Save is debounced, so briefly holding the lock across the write is acceptable.
/// </threadsafety>
public sealed class PresetIndex
{
    private readonly string _path;
    private readonly List<PresetMetadata> _entries = new();
    private readonly List<string> _recentList = new();
    private readonly PresetIndexSettings _settings = new();
    private readonly object _sync = new();

    // Debounce timer — rapid mutations (e.g. LoadBuiltIns calling AddEntry
    // 150+ times) are coalesced into a single disk write 500 ms after the last
    // mutation.  Callers that need an immediate flush (e.g. app exit) can call
    // Save() directly.
    private readonly TimeSpan _saveDebounce = TimeSpan.FromMilliseconds(500);
    private System.Threading.Timer? _saveTimer;

    public IReadOnlyList<PresetMetadata> Entries => _entries;
    public IReadOnlyList<string> RecentList => _recentList;
    public PresetIndexSettings Settings => _settings;

    /// <summary>Fired on the calling thread when <see cref="Save"/> fails to persist the index.
    /// The UI layer can subscribe to show a one-time warning to the user.</summary>
    public event Action<string>? SaveFailed;

    public PresetIndex(string path)
    {
        _path = path;
        Load();
    }

    public PresetMetadata? GetMetadata(string presetName)
        => _entries.FirstOrDefault(m => m.Name == presetName);

    public void UpdateMetadata(string presetName, Action<PresetMetadata> modify)
    {
        lock (_sync)
        {
            var meta = GetMetadata(presetName);
            if (meta == null) return;
            modify(meta);
            meta.Modified = DateTime.UtcNow;
        }
        ScheduleSave();
    }

    public void AddEntry(string presetName)
    {
        lock (_sync)
        {
            if (_entries.Any(m => m.Name == presetName)) return;
            _entries.Add(new PresetMetadata { Name = presetName });
        }
        ScheduleSave();
    }

    public void RemoveEntry(string presetName)
    {
        lock (_sync)
        {
            _entries.RemoveAll(m => m.Name == presetName);
            _recentList.Remove(presetName);
        }
        ScheduleSave();
    }

    public void RenameEntry(string oldName, string newName)
    {
        lock (_sync)
        {
            var meta = GetMetadata(oldName);
            if (meta == null) return;
            meta.Name = newName;
            meta.Modified = DateTime.UtcNow;

            for (int i = 0; i < _recentList.Count; i++)
            {
                if (_recentList[i] == oldName)
                    _recentList[i] = newName;
            }
        }
        ScheduleSave();
    }

    public void RecordUsage(string presetName)
    {
        lock (_sync)
        {
            var meta = GetMetadata(presetName);
            if (meta == null) return;

            meta.LastUsed = DateTime.UtcNow;
            meta.UseCount++;
            meta.Modified = DateTime.UtcNow;

            _recentList.Remove(presetName);
            _recentList.Insert(0, presetName);

            while (_recentList.Count > _settings.MaxRecentCount)
                _recentList.RemoveAt(_recentList.Count - 1);
        }
        ScheduleSave();
    }

    public void ToggleFavorite(string presetName)
    {
        lock (_sync)
        {
            var meta = GetMetadata(presetName);
            if (meta == null) return;
            meta.IsFavorite = !meta.IsFavorite;
            meta.Modified = DateTime.UtcNow;
        }
        ScheduleSave();
    }

    public IEnumerable<PresetMetadata> GetFavorites()
        => _entries.Where(m => m.IsFavorite);

    public IEnumerable<PresetMetadata> GetByCategory(string category)
        => _entries.Where(m => m.Category == category);

    public IEnumerable<PresetMetadata> GetByTag(string tag)
        => _entries.Where(m => m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

    public IEnumerable<PresetMetadata> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _entries;
        var q = query.Trim();
        return _entries.Where(m =>
            m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            m.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            m.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Coalesce rapid mutations into a single disk write.
    /// Each call resets the 500 ms timer; when it fires, <see cref="Save"/>
    /// is invoked once.  Call <see cref="Save"/> directly for an immediate flush.</summary>
    private void ScheduleSave()
    {
        lock (_sync)
        {
            _saveTimer?.Dispose();
            _saveTimer = new System.Threading.Timer(
                _ => Save(), null, _saveDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void Save()
    {
        // Everything below — the JSON serialization AND the disk write — runs
        // under the lock.  This serialises a debounced Save (timer thread) against
        // concurrent UI-thread mutations so the enumerator inside SerializeObject
        // never sees a collection change ("Collection was modified"), keeps an
        // in-place item edit out of serialization, and prevents two overlapping
        // Save calls from racing the same .tmp / File.Replace.
        lock (_sync)
        {
            // Cancel any pending debounced save — we're writing now.
            _saveTimer?.Dispose();
            _saveTimer = null;

            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var data = new IndexData
                {
                    Version = "1.0",
                    Entries = _entries,
                    RecentList = _recentList,
                    Settings = _settings
                };

                var tmpPath = _path + ".tmp";
                File.WriteAllText(tmpPath, JsonConvert.SerializeObject(data, Formatting.Indented));

                // Keep a .bak of the last known-good index so a corrupt
                // load can be recovered without a full rescan.
                if (File.Exists(_path))
                {
                    try
                    {
                        var bakPath = _path + ".bak";
                        File.Copy(_path, bakPath, overwrite: true);
                    }
                    catch { /* .bak is best-effort — never blocks the real save */ }

                    File.Replace(tmpPath, _path, null);
                }
                else File.Move(tmpPath, _path);
                AppLog.Debug("[PresetIndex] Saved {Count} entries to {Path}", _entries.Count, _path);
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "[PresetIndex] Failed to save to {Path}", _path);
                // Notify subscribers (UI) so the user is aware of data-loss risk.
                try { SaveFailed?.Invoke(_path); } catch { /* subscriber must not crash us */ }
            }
        }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var data = JsonConvert.DeserializeObject<IndexData>(json);
            if (data == null) return;

            _entries.Clear();
            if (data.Entries != null) _entries.AddRange(data.Entries);

            _recentList.Clear();
            if (data.RecentList != null) _recentList.AddRange(data.RecentList);

            if (data.Settings != null)
            {
                _settings.MaxRecentCount = data.Settings.MaxRecentCount;
                _settings.StartupBehavior = data.Settings.StartupBehavior;
            }
            AppLog.Debug("[PresetIndex] Loaded {Count} entries from {Path}", _entries.Count, _path);
        }
        catch (Exception ex)
        {
            // Corrupt file — preserve a .corrupt copy for diagnostics, then
            // try to recover from the .bak (last known-good, written by Save).
            AppLog.Warning(ex, "[PresetIndex] Failed to load {Path}, attempting .bak recovery", _path);
            try
            {
                if (File.Exists(_path))
                {
                    var corruptPath = _path + ".corrupt";
                    if (File.Exists(corruptPath)) File.Delete(corruptPath);
                    File.Move(_path, corruptPath);
                    AppLog.Debug("[PresetIndex] Corrupt index preserved at {Path}", corruptPath);
                }
            }
            catch { /* backup failure is non-critical */ }

            // Attempt recovery from .bak
            var bakPath = _path + ".bak";
            if (File.Exists(bakPath))
            {
                try
                {
                    var bakJson = File.ReadAllText(bakPath);
                    var bakData = JsonConvert.DeserializeObject<IndexData>(bakJson);
                    if (bakData != null)
                    {
                        _entries.Clear();
                        if (bakData.Entries != null) _entries.AddRange(bakData.Entries);
                        _recentList.Clear();
                        if (bakData.RecentList != null) _recentList.AddRange(bakData.RecentList);
                        if (bakData.Settings != null)
                        {
                            _settings.MaxRecentCount = bakData.Settings.MaxRecentCount;
                            _settings.StartupBehavior = bakData.Settings.StartupBehavior;
                        }
                        AppLog.Information("[PresetIndex] Recovered {Count} entries from .bak", _entries.Count);
                        return; // recovered — skip the reset below
                    }
                }
                catch (Exception bakEx)
                {
                    AppLog.Warning(bakEx, "[PresetIndex] .bak recovery also failed, resetting to defaults");
                }
            }

            _entries.Clear();
            _recentList.Clear();
        }
    }

    private class IndexData
    {
        public string Version { get; set; } = "1.0";
        public List<PresetMetadata> Entries { get; set; } = new();
        public List<string> RecentList { get; set; } = new();
        public PresetIndexSettings Settings { get; set; } = new();
    }
}
