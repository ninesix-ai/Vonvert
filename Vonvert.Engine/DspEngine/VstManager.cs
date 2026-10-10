// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Manages VST 2.x plugin discovery, loading, and lifecycle.
 *
 * Responsibilities:
 *   - Scan default VST directories (Program Files, Common Files) and custom
 *     user-specified paths for .dll files.
 *   - Maintain a registry of discovered plugins (VstPluginInfo).
 *   - Load plugins on demand, returning VstEffect instances that plug
 *     directly into the DSPChain.
 *   - Track all loaded effects for centralised disposal.
 *
 * Thread-safety:
 *   - Scan operations and plugin loading are guarded by a lock.
 *   - The returned VstEffect instances are NOT thread-safe for Process();
 *     they must be driven exclusively by the audio thread via DSPChain.
 */
public sealed class VstManager : IDisposable
{
    private static readonly Lazy<VstManager> _instance = new(() => new VstManager());
    public static VstManager Instance => _instance.Value;

    private readonly List<VstPluginInfo>          _discovered = new();
    private readonly Dictionary<string, VstEffect> _loaded    = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>True after at least one scan has completed.</summary>
    public bool HasScanned => _scanned;
    private bool _scanned;

    /// <summary>All discovered plugin descriptors (does not imply they are loaded).</summary>
    public IReadOnlyList<VstPluginInfo> DiscoveredPlugins
    {
        get { lock (_lock) return _discovered.ToList(); }
    }

    /// <summary>All currently loaded and active VST effects.</summary>
    public IReadOnlyDictionary<string, VstEffect> LoadedEffects
    {
        get { lock (_lock) return new Dictionary<string, VstEffect>(_loaded, StringComparer.OrdinalIgnoreCase); }
    }

    public event Action<VstPluginInfo>? PluginDiscovered;
    public event Action<string>?        PluginLoaded;
    public event Action<string>?        PluginUnloaded;

    // =====================================================================
    // Scanning
    // =====================================================================

    /// <summary>Scan default and custom directories for VST2 .dll files.</summary>
    public void ScanForPlugins(params string[] additionalPaths)
    {
        lock (_lock)
        {
            var dirs = GetDefaultSearchPaths();
            dirs.AddRange(additionalPaths.Where(p => !string.IsNullOrWhiteSpace(p)));

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var dll in Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories))
                    {
                        if (_discovered.Any(d => d.DllPath.Equals(dll, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var info = TryPeekPlugin(dll);
                        if (info != null)
                        {
                            _discovered.Add(info);
                            PluginDiscovered?.Invoke(info);
                            AppLog.Information("[VST] Discovered: {Name} ({Path})", info.Name, info.DllPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warning(ex, "[VST] Scan error in {Dir}", dir);
                }
            }

            _scanned = true;
        }
    }

    /// <summary>Register a single DLL as a known plugin (e.g. from a preset reference).</summary>
    public VstPluginInfo? RegisterPlugin(string dllPath)
    {
        if (!File.Exists(dllPath)) return null;
        lock (_lock)
        {
            var existing = _discovered.FirstOrDefault(d => d.DllPath.Equals(dllPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var info = TryPeekPlugin(dllPath);
            if (info != null)
            {
                _discovered.Add(info);
                PluginDiscovered?.Invoke(info);
            }
            return info;
        }
    }

    // =====================================================================
    // Loading
    // =====================================================================

    /// <summary>Load a VST plugin by DLL path. Returns the VstEffect (cached if already loaded).</summary>
    public VstEffect? LoadPlugin(string dllPath)
    {
        if (!ValidatePluginPath(dllPath))
        {
            AppLog.Warning("[VST] Rejected plugin path (security): {Path}", dllPath);
            return null;
        }

        lock (_lock)
        {
            if (_disposed) return null;
            if (_loaded.TryGetValue(dllPath, out var existing)) return existing;

            try
            {
                var effect = new VstEffect(dllPath);
                _loaded[dllPath] = effect;
                PluginLoaded?.Invoke(dllPath);
                AppLog.Information("[VST] Loaded: {Product} by {Vendor} ({Path})",
                    effect.Product, effect.Vendor, dllPath);
                return effect;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "[VST] Load failed: {Path}", dllPath);
                return null;
            }
        }
    }

    /// <summary>
    /// Validate that a plugin path is safe to load.
    /// Rejects paths with traversal attempts, system directories, or non-DLL extensions.
    /// </summary>
    internal static bool ValidatePluginPath(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath)) return false;
        if (!File.Exists(dllPath)) return false;

        // Must be a .dll file
        if (!dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            var fullPath = Path.GetFullPath(dllPath);

            // Reject paths in Windows system directories
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var windowsDir = Path.GetDirectoryName(systemDir) ?? systemDir;
            if (fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
                return false;

            // Reject paths with suspicious traversal patterns
            if (fullPath.Contains("..\\") || fullPath.Contains("../"))
                return false;

            // Reject very small files (likely not valid plugins)
            var fi = new FileInfo(fullPath);
            if (fi.Length < 1024) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Unload a plugin, disposing the VstEffect.</summary>
    public void UnloadPlugin(string dllPath)
    {
        lock (_lock)
        {
            if (!_loaded.Remove(dllPath, out var effect)) return;
            try { effect.Dispose(); } catch { /* VST dispose is best-effort */ }
            PluginUnloaded?.Invoke(dllPath);
        }
    }

    /// <summary>Unload all currently loaded plugins.</summary>
    public void UnloadAll()
    {
        lock (_lock)
        {
            foreach (var kvp in _loaded.ToList())
            {
                try { kvp.Value.Dispose(); } catch { /* VST dispose is best-effort */ }
                PluginUnloaded?.Invoke(kvp.Key);
            }
            _loaded.Clear();
        }
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnloadAll();
    }

    // =====================================================================
    // Internal
    // =====================================================================

    /// <summary>
    /// Attempt to read basic metadata from a DLL without fully loading the VST plugin.
    /// Uses LoadLibraryEx with LOAD_LIBRARY_AS_DATAFILE to avoid executing DllMain,
    /// preventing arbitrary code execution from untrusted DLLs during discovery.
    /// </summary>
    private VstPluginInfo? TryPeekPlugin(string dllPath)
    {
        try
        {
            IntPtr handle = Vst2Native.LoadLibraryEx(dllPath, IntPtr.Zero, Vst2Native.LOAD_LIBRARY_AS_DATAFILE);
            if (handle == IntPtr.Zero) return null;

            // With LOAD_LIBRARY_AS_DATAFILE, we cannot resolve exports.
            // Just validate the file exists and has a reasonable size.
            // The actual VST entry-point check happens during LoadPlugin().
            Vst2Native.FreeLibrary(handle);

            var fi = new FileInfo(dllPath);
            if (fi.Length < 1024) return null; // Too small to be a valid VST plugin

            return new VstPluginInfo
            {
                Name    = fi.Name,
                DllPath = fi.FullName,
                Size    = fi.Length
            };
        }
        catch
        {
            return null;
        }
    }

    internal static List<string> GetDefaultSearchPaths()
    {
        var paths = new List<string>();
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var cf = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);

        // Standard VST2 search locations on Windows
        paths.Add(Path.Combine(pf,  "Steinberg", "VstPlugins"));
        paths.Add(Path.Combine(pf,  "Common Files", "VST2"));
        paths.Add(Path.Combine(pf,  "Common Files", "VSTPlugins"));
        paths.Add(Path.Combine(cf,  "VST2"));
        paths.Add(Path.Combine(cf,  "VSTPlugins"));

        // 32-bit paths on 64-bit OS
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pf86))
        {
            paths.Add(Path.Combine(pf86, "Steinberg", "VstPlugins"));
            paths.Add(Path.Combine(pf86, "Common Files", "VST2"));
            paths.Add(Path.Combine(pf86, "Common Files", "VSTPlugins"));
        }

        // User-local VST folder
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        paths.Add(Path.Combine(localAppData, "VST2"));

        // Documents/VSTPlugins
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        paths.Add(Path.Combine(docs, "VSTPlugins"));
        paths.Add(Path.Combine(docs, "VST2"));

        return paths;
    }
}

// =========================================================================
// VstPluginInfo — metadata about a discovered VST plugin
// =========================================================================

/// <summary>Describes a discovered VST2 plugin (may or may not be loaded).</summary>
public class VstPluginInfo
{
    /// <summary>Display name (DLL file name).</summary>
    public string Name    { get; set; } = "";

    /// <summary>Absolute path to the plugin DLL.</summary>
    public string DllPath { get; set; } = "";

    /// <summary>Vendor name (populated after loading).</summary>
    public string Vendor  { get; set; } = "";

    /// <summary>Product name (populated after loading).</summary>
    public string Product { get; set; } = "";

    /// <summary>DLL file size in bytes.</summary>
    public long   Size    { get; set; }

    public override string ToString() => string.IsNullOrEmpty(Vendor) ? Name : $"{Product} ({Vendor})";
}

// =========================================================================
// VstPluginConfig — serialisable VST slot configuration for presets
// =========================================================================

/// <summary>
/// Persisted configuration for a single VST plugin slot within a VoiceProfile.
/// Stores the DLL path, enabled state, and all parameter values so that
/// presets can be saved/loaded with their VST chain intact.
/// </summary>
public class VstPluginConfig
{
    /// <summary>Absolute path to the VST2 plugin DLL.</summary>
    public string DllPath   { get; set; } = "";

    /// <summary>Whether the effect is enabled.</summary>
    public bool   IsEnabled { get; set; } = true;

    /// <summary>Snapshot of all parameter values (normalised 0..1).</summary>
    public List<float> Parameters { get; set; } = new();
}
