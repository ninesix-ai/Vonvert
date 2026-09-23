// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the structural invariants that keep presets applying correctly: every
// default-chain effect is resolvable, the chain starts with DCOffset and ends
// with LoudnessMeter, exactly the safety effects are enabled by default, and the
// Robot/Drive effects stay registered.

namespace Vonvert.Tests.DSP;

using System;
using System.Linq;
using Vonvert.Engine.DspEngine;
using Xunit;

public class EffectRegistryTests
{
    [Fact(DisplayName = "ER-001: DefaultChain names are all resolvable in factory")]
    public void ER001_Factory_Covers_DefaultChain()
    {
        var allNames = new System.Collections.Generic.HashSet<string>(EffectRegistry.AllNames, StringComparer.Ordinal);
        var chain = EffectRegistry.CreateDefault();
        foreach (var fx in chain.Effects)
            Assert.True(allNames.Contains(fx.Name),
                $"DefaultChain contains '{fx.Name}' which is not in EffectRegistry.AllNames");
    }

    [Fact(DisplayName = "ER-002: Unknown effect name returns null")]
    public void ER002_Create_Unknown_Returns_Null()
    {
        Assert.Null(EffectRegistry.Create("NonExistentEffect"));
        Assert.Null(EffectRegistry.Create(""));
        Assert.Null(EffectRegistry.Create("Watermark"));   // not a registered effect in this build
    }

    [Fact(DisplayName = "ER-003: Default chain starts with DCOffset and ends with LoudnessMeter")]
    public void ER003_CreateDefault_Order_Preserved()
    {
        var chain = EffectRegistry.CreateDefault();
        Assert.NotEmpty(chain.Effects);
        Assert.Equal("DCOffset", chain.Effects[0].Name);
        Assert.Equal("LoudnessMeter", chain.Effects[^1].Name);
    }

    [Fact(DisplayName = "ER-004: Exactly the five safety effects are enabled by default")]
    public void ER004_CreateDefault_DefaultOn_Effects()
    {
        var chain = EffectRegistry.CreateDefault();
        var enabled = chain.Effects.Where(e => e.IsEnabled).Select(e => e.Name).ToList();
        Assert.Equal(new[] { "DCOffset", "VoiceGate", "VoiceCompressor", "VoiceLimiter", "LoudnessMeter" }, enabled);
    }

    [Fact(DisplayName = "ER-005: The remaining effects default to disabled")]
    public void ER005_CreateDefault_DefaultOff_Effects()
    {
        var chain = EffectRegistry.CreateDefault();
        int disabled = chain.Effects.Count(e => !e.IsEnabled);
        Assert.True(disabled >= 8, $"Expected >= 8 disabled effects in the 14-effect chain, got {disabled}");
    }

    [Fact(DisplayName = "ER-006: DSPChain.CreateEffectByName delegates to EffectRegistry")]
    public void ER006_BackwardCompat_CreateEffectByName()
    {
        var gate1 = DSPChain.CreateEffectByName("VoiceGate");
        var gate2 = EffectRegistry.Create("VoiceGate");
        Assert.NotNull(gate1);
        Assert.NotNull(gate2);
        Assert.Equal(gate1!.GetType(), gate2!.GetType());
        Assert.Null(DSPChain.CreateEffectByName("NonExistent"));
    }

    [Fact(DisplayName = "ER-007: DSPChain.CreateDefault matches EffectRegistry.CreateDefault count and order")]
    public void ER007_BackwardCompat_CreateDefault()
    {
        var chainNames = DSPChain.CreateDefault().Effects.Select(e => e.Name).ToList();
        var registryNames = EffectRegistry.CreateDefault().Effects.Select(e => e.Name).ToList();
        Assert.Equal(chainNames.Count, registryNames.Count);
        Assert.Equal(chainNames, registryNames);
    }

    [Fact(DisplayName = "ER-008: Core and ported effects stay registered")]
    public void ER008_AllNames_KeyEffects()
    {
        var names = EffectRegistry.AllNames;
        // Floor guards against accidental removals; the ported Robot/Drive effects
        // (free-preset tier) must stay present.
        Assert.True(names.Count >= 12, $"Expected >= 12 effects, got {names.Count}");
        foreach (var key in new[] { "VoicePitch", "VoiceEQ", "VoiceGate", "VoiceLimiter", "VoiceRobot", "VoiceDrive", "LoudnessMeter" })
            Assert.Contains(key, names);
    }

    [Fact(DisplayName = "ER-009: All created effects implement IAudioEffect")]
    public void ER009_AllEffects_Implement_IAudioEffect()
    {
        foreach (var name in EffectRegistry.AllNames)
        {
            var effect = EffectRegistry.Create(name);
            Assert.NotNull(effect);
            Assert.IsAssignableFrom<IAudioEffect>(effect);
        }
    }
}
