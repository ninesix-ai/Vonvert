// SPDX-License-Identifier: MIT
// Copyright © ninesix-ai studio

using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Centralised default values for DSP effect parameters.
/// Values are loaded from the embedded <c>dsp-defaults.json</c> resource
/// so they can be adjusted without recompiling.
/// If the JSON is missing or corrupt, hardcoded fallbacks are used.
/// </summary>
public static class DspDefaults
{
    // ── JSON model ────────────────────────────────────────────────────

    private sealed class BasicModel
    {
        public float PreAmpGain { get; set; } = 1.0f;
    }

    private sealed class DspDefaultsModel
    {
        public BasicModel Basic { get; set; } = new();
    }

    // ── Loading ───────────────────────────────────────────────────────

    private const string EmbeddedResourceName =
        "Vonvert.Engine.PresetLibrary.dsp-defaults.json";

    private static readonly Lazy<DspDefaultsModel> _data = new(LoadDefaults);

    private static DspDefaultsModel LoadDefaults()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null)
            {
                AppLog.Warning(
                    "[DspDefaults] Embedded resource '{0}' not found — using hardcoded fallbacks.",
                    EmbeddedResourceName);
                return new DspDefaultsModel();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var model = JsonConvert.DeserializeObject<DspDefaultsModel>(json);
            return model ?? new DspDefaultsModel();
        }
        catch (Exception ex)
        {
            AppLog.Warning(
                "[DspDefaults] Failed to load JSON defaults: {0} — using hardcoded fallbacks.",
                ex.Message);
            return new DspDefaultsModel();
        }
    }

    // ── Gain ──────────────────────────────────────────────────────────

    public static float PreAmpGain => _data.Value.Basic.PreAmpGain;
}
