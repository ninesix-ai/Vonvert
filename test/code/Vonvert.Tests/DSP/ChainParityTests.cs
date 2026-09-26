// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// The engine assembles its chain twice, from two different places: the live
// real-time path comes from EffectRegistry.CreateDefault(), while offline
// rendering (RecordingExporter, batch bounce) builds its own chain through
// VoiceProfile.CreateDSPChain(). Nothing else keeps those two in step, so a
// stage that exists in one and not the other silently changes what a user hears
// versus what they export — the live chain's limiter being the concrete case.
// These cases pin that a profile with every effect switch on reproduces the
// canonical live chain exactly, in the same signal-flow order.

namespace Vonvert.Tests.DSP;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class ChainParityTests
{
    private static List<string> NamesOf(Vonvert.Engine.DspEngine.DSPChain chain)
        => chain.Effects.Select(e => e.Name).ToList();

    /// <summary>
    /// A profile with every effect enabled, discovered reflectively so a future
    /// effect cannot hide from this guard: any public bool *Enabled property on
    /// VoiceProfile is switched on here.
    /// </summary>
    private static VoiceProfile AllEffectsOn()
    {
        var p = new VoiceProfile { Name = "parity" };
        foreach (var prop in typeof(VoiceProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType == typeof(bool)
                && prop.Name.EndsWith("Enabled", StringComparison.Ordinal)
                && prop.CanWrite)
            {
                prop.SetValue(p, true);
            }
        }
        return p;
    }

    [Fact(DisplayName = "CHAIN-P-01: all-effects profile builds exactly the live default chain's effect set")]
    public void OfflineChain_Covers_DefaultChain()
    {
        var live = NamesOf(EffectRegistry.CreateDefault());
        var offline = NamesOf(AllEffectsOn().CreateDSPChain());

        var missing = live.Except(offline).ToList();
        var extra = offline.Except(live).ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"offline render path differs from live path: live={live.Count} effects, " +
            $"offline={offline.Count}; missing offline=[{string.Join(", ", missing)}], " +
            $"absent from live=[{string.Join(", ", extra)}]");
    }

    [Fact(DisplayName = "CHAIN-P-02: offline chain preserves the canonical signal-flow order")]
    public void OfflineChain_Preserves_CanonicalOrder()
    {
        Assert.Equal(NamesOf(EffectRegistry.CreateDefault()), NamesOf(AllEffectsOn().CreateDSPChain()));
    }

    [Fact(DisplayName = "CHAIN-P-03: limiter is present and enabled on both render paths")]
    public void Limiter_PresentAndEnabled_OnBothPaths()
    {
        var live = EffectRegistry.CreateDefault().Effects.OfType<VxLimiter>().SingleOrDefault();
        var offline = AllEffectsOn().CreateDSPChain().Effects.OfType<VxLimiter>().SingleOrDefault();

        Assert.NotNull(live);
        Assert.NotNull(offline);
        Assert.True(live!.IsEnabled, "live chain must keep the limiter enabled");
        Assert.True(offline!.IsEnabled, "exported audio must be limited like monitored audio");
    }
}
