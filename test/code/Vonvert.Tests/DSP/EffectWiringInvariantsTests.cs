// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Structural guards for the five-integration-point effect contract. An effect
// that is registered but never mapped by DspParameterCoordinator silently fails
// to take effect when a preset is applied — the panel still moves, the tests
// still pass, and the user hears nothing. INV-01 pins that everything the
// default chain carries is either mapped or explicitly exempt; INV-02 pins that
// a factory key equals the effect's own Name, which is what chain lookups and
// presets match on.

namespace Vonvert.Tests.DSP;

using System.Linq;
using Vonvert.Engine.DspEngine;
using Xunit;

public class EffectWiringInvariantsTests
{
    /// <summary>
    /// Chain members that legitimately carry no user parameters and therefore are
    /// not mapped by the coordinator. Adding an effect here must come with a
    /// reason; a parameterised effect belongs in the coordinator, not here.
    /// </summary>
    private static readonly HashSet<string> NotParameterMapped = new()
    {
        "DCOffset",       // always-on safety filter
        "VoiceLimiter",   // fixed protection stage
        "LoudnessMeter",  // metering only
    };

    [Fact(DisplayName = "INV-01: every default-chain effect is parameter-mapped or explicitly exempt")]
    public void DefaultChain_Effects_AreAllAccountedFor()
    {
        var (engine, coord) = DspTestRig.New();
        var chainNames = engine.Effects.Effects.Select(f => f.Name).ToHashSet();
        var managed = coord.ManagedEffectNames.ToHashSet();

        var unmapped = chainNames.Except(managed).Except(NotParameterMapped).OrderBy(n => n).ToList();
        Assert.True(unmapped.Count == 0,
            "effects are in the default chain but never pushed by ApplyPreset, so presets " +
            $"would silently ignore them: [{string.Join(", ", unmapped)}]");
    }

    [Fact(DisplayName = "INV-02: EffectRegistry factory keys match the created effect's own Name")]
    public void Registry_Keys_MatchEffectNames()
    {
        foreach (var key in EffectRegistry.AllNames)
        {
            var fx = EffectRegistry.Create(key);
            Assert.NotNull(fx);
            Assert.True(key == fx!.Name,
                $"factory key '{key}' creates an effect reporting '{fx.Name}'; chain lookups " +
                "and presets match on Name, so the two must agree");
        }
    }
}
