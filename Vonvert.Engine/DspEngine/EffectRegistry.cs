// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;

namespace Vonvert.Engine.DspEngine;

/// <summary>
/// Central registry of all DSP effects known to the engine.
/// Single authority for "what effects exist" and "what the default chain looks like".
/// <see cref="DSPChain"/> delegates to this class for effect instantiation.
/// </summary>
public static class EffectRegistry
{
    // ── Factory ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Func<IAudioEffect>> Factories = new()
    {
        ["DCOffset"]            = () => new DCOffsetFilter(),
        ["VoiceGate"]           = () => new VxGate(),
        ["VoiceNoiseReduce"]    = () => new NoiseReductionEffect(),
        ["VoicePitch"]          = () => new VxPitch(),
        ["VoiceDeesser"]        = () => new DeesserEffect(),
        ["VoiceEQ"]             = () => new VxEq(),
        ["VoiceRobot"]          = () => new VxRobot(),
        ["VoiceDrive"]          = () => new VxDrive(),
        ["VoiceChorus"]         = () => new VxChorus(),
        ["VoiceFlanger"]        = () => new FlangerEffect(),
        ["VoicePhaser"]         = () => new PhaserEffect(),
        ["VoiceTremolo"]        = () => new TremoloEffect(),
        ["VoiceVibrato"]        = () => new VibratoEffect(),
        ["VoiceDelay"]          = () => new DelayEffect(),
        ["VoiceReverb"]         = () => new VxReverb(),
        ["VoiceCompressor"]     = () => new VxCompressor(),
        ["VoiceLimiter"]        = () => new VxLimiter(),
        ["TiltEQ"]              = () => new TiltEQEffect(),
        ["GraphicEQ"]           = () => new GraphicEQEffect(),
        ["Bitcrusher"]          = () => new BitcrusherEffect(),
        ["LoudnessMeter"]       = () => new LoudnessMeterEffect(),
    };

    // ── Default chain ────────────────────────────────────────────────────
    // Ordered by canonical signal flow.
    // (effect factory key, default enabled?)

    private static readonly (string Name, bool DefaultOn)[] DefaultChainEntries =
    {
        ("DCOffset",            true),
        ("VoiceGate",           true),
        ("VoiceNoiseReduce",    false),
        ("VoicePitch",          false),
        ("VoiceDeesser",        false),
        ("VoiceEQ",             false),
        ("TiltEQ",              false),
        ("GraphicEQ",           false),
        ("VoiceRobot",          false),
        ("VoiceDrive",          false),
        ("Bitcrusher",          false),
        ("VoiceChorus",         false),
        ("VoiceFlanger",        false),
        ("VoicePhaser",         false),
        ("VoiceVibrato",        false),
        ("VoiceTremolo",        false),
        ("VoiceDelay",          false),
        ("VoiceReverb",         false),
        ("VoiceCompressor",     true),
        ("VoiceLimiter",        true),
        ("LoudnessMeter",       true),
    };

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>All registered effect names (current build configuration).</summary>
    public static IReadOnlyList<string> AllNames => Factories.Keys.ToList().AsReadOnly();

    /// <summary>Create an effect instance by name; returns <c>null</c> for unknown or unavailable effects.</summary>
    public static IAudioEffect? Create(string name)
        => Factories.TryGetValue(name, out var factory) ? factory() : null;

    /// <summary>Build the default DSP chain (canonical signal-flow order).</summary>
    public static DSPChain CreateDefault(EngineStats? metrics = null)
    {
        var chain = metrics != null ? new DSPChain(metrics) : new DSPChain();
        foreach (var (name, defaultOn) in DefaultChainEntries)
        {
            var fx = Create(name);
            if (fx != null)
            {
                fx.IsEnabled = defaultOn;
                chain.Add(fx);
            }
        }
        return chain;
    }
}
