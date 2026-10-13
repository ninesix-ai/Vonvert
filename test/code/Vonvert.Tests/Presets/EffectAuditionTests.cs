// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Tests.Presets;

using System;
using System.Linq;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Engine.Synthesis;
using Xunit;

/// <summary>
/// EffectAudition unit tests — AUD-001 ~ AUD-004.
/// Proves a single-effect audition clip is isolated to the chosen effect, actually
/// alters the signal, and never mutates the source profile.
/// </summary>
public class EffectAuditionTests
{
    private static DspGroup Group(string nameKey)
        => DspParameterCatalog.Groups.Single(g => g.NameKey == nameKey);

    [Fact(DisplayName = "AUD-001: single-effect chain holds exactly DCOffset + one effect + limiter")]
    public void Chain_Is_Isolated_To_Target_Effect()
    {
        var profile = new VoiceProfile();
        var chain = EffectAudition.BuildSingleEffectChain(profile, Group("GrpBitcrusher"));

        var names = chain.Effects.Select(e => e.Name).ToList();
        // DCOffset and VoiceLimiter are the unconditional safety stages; nothing else may appear.
        Assert.Equal(3, names.Count);
        Assert.Contains("DCOffset", names);
        Assert.Contains("VoiceLimiter", names);
        Assert.Contains("Bitcrusher", names);
    }

    [Fact(DisplayName = "AUD-002: audition actually changes the signal for an audible effect")]
    public void Audition_Changes_Signal()
    {
        // SpeechSynthesizer is deterministic (fixed seed); the dry and processed
        // signals start identical, so any difference is caused by the effect.
        var profile = new VoiceProfile { BitcrusherBitDepth = 4f, BitcrusherSampleReduction = 3f };

        var raw = SpeechSynthesizer.Generate(AudioConstants.EngineRate, EffectAudition.DurationSec);
        var wet = (float[])raw.Clone();

        var chain = EffectAudition.BuildSingleEffectChain(profile, Group("GrpBitcrusher"));
        chain.Process(wet.AsSpan());

        Assert.False(raw.SequenceEqual(wet), "bitcrusher audition must differ from the dry signal");
    }

    [Fact(DisplayName = "AUD-003: audition does not mutate the source profile")]
    public void Audition_Does_Not_Mutate_Source()
    {
        var profile = new VoiceProfile { GateEnabled = true, CompressEnabled = true, RobotEnabled = true };
        profile.VstPlugins.Add(new VstPluginConfig { DllPath = "x.dll", IsEnabled = true });
        var before = new[] { profile.GateEnabled, profile.CompressEnabled, profile.RobotEnabled };
        var vstBefore = profile.VstPlugins.Count;

        _ = EffectAudition.GeneratePreviewBytes(profile, Group("GrpBitcrusher"));

        Assert.Equal(before, new[] { profile.GateEnabled, profile.CompressEnabled, profile.RobotEnabled });
        Assert.Equal(vstBefore, profile.VstPlugins.Count);
    }

    [Fact(DisplayName = "AUD-004: a group with no effect to audition is rejected")]
    public void Gain_Group_Is_Rejected()
    {
        var profile = new VoiceProfile();
        Assert.Throws<ArgumentException>(
            () => EffectAudition.BuildSingleEffectChain(profile, Group("GrpGain")));
    }
}
