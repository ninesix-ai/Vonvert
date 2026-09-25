// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vonvert.Engine.Services;

/// <summary>
/// Centralised application configuration.
/// Loads from %APPDATA%/Vonvert/app-config.json with built-in defaults
/// for every key. Missing file or missing keys are silently handled —
/// the app always starts with sensible defaults.
///
/// Sections:
///   ui       — window chrome, VU meters
///   network  — HTTP timeouts, update checker, retry policy
/// </summary>
public sealed class AppConfig
{
    // ── Section models ──────────────────────────────────────────────

    /// <summary>UI behaviour parameters (window chrome, meters).</summary>
    public sealed class UiSection
    {
        public int    ResizeBorderWidth   { get; set; } = 8;
        public double VuMeterMaxWidth     { get; set; } = 156;
        public bool   RoleSystemEnabled   { get; set; } = true;
    }

    /// <summary>Network behaviour parameters (HTTP, update checker, retry).</summary>
    public sealed class NetworkSection
    {
        public int    HttpTimeoutSeconds          { get; set; } = 10;
        public int    UpdateCheckIntervalHours    { get; set; } = 24;
        public string UpdateAnnouncementApi       { get; set; } = "https://api.github.com/repos/ninesix-ai/Vonvert/releases";
        public string UpdateAnnouncementPage      { get; set; } = "https://github.com/ninesix-ai/Vonvert/releases";
        public int    RetryMaxAttempts            { get; set; } = 3;
        public double RetryInitialDelaySeconds    { get; set; } = 1.0;
        public double RetryMaxDelaySeconds        { get; set; } = 4.0;
    }

    /// <summary>Audio device selection (persisted so the user's mic / VB-Cable
    /// choice survives restarts).</summary>
    public sealed class AudioSection
    {
        public string InputDeviceId   { get; set; } = string.Empty;
        public string OutputDeviceId  { get; set; } = string.Empty;
        /// <summary>When true, soundboard pads also broadcast through the engine
        /// (heard by others); when false, pads play as local-only auditions.</summary>
        public bool   SoundboardLiveMode { get; set; } = false;
    }

    /// <summary>Role/persona state persisted under the "persona" JSON key.
    /// Plain POCO so RoleProfileService can Load/Save against a fresh instance in tests.</summary>
    public sealed class PersonaSection
    {
        public List<string> ActivePersonas { get; set; } = new();
        public string?      ComplexityLevelOverride { get; set; }
    }

    // ── Singleton ───────────────────────────────────────────────────

    private static readonly Lazy<AppConfig> _lazy = new(() => new AppConfig());

    /// <summary>Global configuration instance. Loads once on first access.</summary>
    public static AppConfig Instance => _lazy.Value;

    // ── Public properties ───────────────────────────────────────────

    public UiSection       Ui       { get; } = new();
    public NetworkSection  Network  { get; } = new();
    public AudioSection    Audio    { get; } = new();
    public PersonaSection  Persona  { get; } = new();

    // ── Persistence ─────────────────────────────────────────────────

    /// <summary>{DataRoot}/app-config.json</summary>
    public static string FilePath => Path.Combine(
        Vonvert.Engine.AppPaths.Root, "app-config.json");

    private AppConfig()
    {
        Load();
    }

    /// <summary>Load user overrides from disk. Missing file = all defaults.</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = SafeFileHelper.ReadAllTextSafe(FilePath);
            if (json == null) return;

            var obj = JObject.Parse(json);

            if (obj["ui"] is JObject uiObj)
                JsonConvert.PopulateObject(uiObj.ToString(), Ui);

            if (obj["network"] is JObject netObj)
                JsonConvert.PopulateObject(netObj.ToString(), Network);

            if (obj["audio"] is JObject audObj)
                JsonConvert.PopulateObject(audObj.ToString(), Audio);

            if (obj["persona"] is JObject perObj)
                JsonConvert.PopulateObject(perObj.ToString(), Persona);

            AppLog.Information("AppConfig: loaded from {0}", FilePath);
        }
        catch (Exception ex)
        {
            AppLog.Warning("AppConfig: failed to load — {0}", ex.Message);
        }
    }

    /// <summary>Persist current values to disk.</summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);

            var obj = new JObject
            {
                ["ui"] = JObject.FromObject(Ui),
                ["network"] = JObject.FromObject(Network),
                ["audio"] = JObject.FromObject(Audio),
                ["persona"] = JObject.FromObject(Persona),
            };

            File.WriteAllText(FilePath, obj.ToString(Formatting.Indented));
        }
        catch (Exception ex)
        {
            AppLog.Warning("AppConfig: failed to save — {0}", ex.Message);
        }
    }
}
