// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Invariant + wiring guards for the declarative parameter catalog that drives the
// expert panel. These are the checks that make the catalog trustworthy as the
// single source of truth: ranges are sane, defaults sit inside their range,
// accessors are populated for the right kind, and every get/set round-trips on a
// real VoiceProfile. A new parameter added to the catalog is covered automatically.

namespace Vonvert.Tests.Presets;

using System.Collections.Generic;
using System.Linq;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class DspParameterCatalogTests
{
    private static IEnumerable<DspParam> AllParams()
        => DspParameterCatalog.Groups.SelectMany(g => g.Params);

    [Fact(DisplayName = "CAT-001: group and param label keys are non-empty and group keys are unique")]
    public void CAT001_KeysValid()
    {
        var groupKeys = new HashSet<string>();
        foreach (var g in DspParameterCatalog.Groups)
        {
            Assert.False(string.IsNullOrWhiteSpace(g.NameKey));
            Assert.True(groupKeys.Add(g.NameKey), $"duplicate group key {g.NameKey}");
            foreach (var p in g.Params)
                Assert.False(string.IsNullOrWhiteSpace(p.LabelKey));
        }
    }

    [Fact(DisplayName = "CAT-002: float params have Min<Max and a default inside [Min,Max]")]
    public void CAT002_FloatRangesValid()
    {
        foreach (var p in AllParams().Where(p => !p.IsBool))
        {
            Assert.True(p.Min < p.Max, $"{p.LabelKey}: min must be < max");
            Assert.InRange(p.Default, p.Min, p.Max);
        }
    }

    [Fact(DisplayName = "CAT-003: accessors match the parameter kind (float vs bool)")]
    public void CAT003_AccessorsPopulated()
    {
        foreach (var p in AllParams())
        {
            if (p.IsBool)
            {
                Assert.NotNull(p.GetB); Assert.NotNull(p.SetB);
                Assert.Null(p.GetF);   Assert.Null(p.SetF);
            }
            else
            {
                Assert.NotNull(p.GetF); Assert.NotNull(p.SetF);
                Assert.Null(p.GetB);    Assert.Null(p.SetB);
            }
        }
    }

    [Fact(DisplayName = "CAT-004: every float param round-trips get/set on a VoiceProfile")]
    public void CAT004_FloatRoundTrip()
    {
        foreach (var p in AllParams().Where(p => !p.IsBool))
        {
            var profile = new VoiceProfile();
            var probe = p.Min + (p.Max - p.Min) * 0.37f;
            p.SetF!(profile, probe);
            Assert.Equal(probe, p.GetF!(profile), 3);
        }
    }

    [Fact(DisplayName = "CAT-005: every bool flag round-trips get/set on a VoiceProfile")]
    public void CAT005_BoolRoundTrip()
    {
        foreach (var p in AllParams().Where(p => p.IsBool))
        {
            var profile = new VoiceProfile();
            p.SetB!(profile, true);
            Assert.True(p.GetB!(profile));
            p.SetB!(profile, false);
            Assert.False(p.GetB!(profile));
        }
    }

    [Fact(DisplayName = "CAT-006: every effect group's enable switch round-trips")]
    public void CAT006_EnableRoundTrip()
    {
        foreach (var g in DspParameterCatalog.Groups.Where(g => g.HasEnable))
        {
            var profile = new VoiceProfile();
            g.SetEnable!(profile, true);
            Assert.True(g.GetEnable!(profile));
            g.SetEnable!(profile, false);
            Assert.False(g.GetEnable!(profile));
        }
    }

    [Fact(DisplayName = "CAT-007: the 10-band graphic EQ exposes exactly 10 independent gain sliders")]
    public void CAT007_GraphicEqBands()
    {
        var geq = DspParameterCatalog.Groups.First(g => g.NameKey == "GrpGraphicEQ");
        Assert.Equal(10, geq.Params.Count);

        var profile = new VoiceProfile();
        geq.Params[3].SetF!(profile, 6.5f);
        Assert.Equal(6.5f, geq.Params[3].GetF!(profile), 3);
        // A neighbouring band must not be affected (independent array slots).
        Assert.Equal(0f, geq.Params[4].GetF!(profile), 3);
    }

    [Fact(DisplayName = "CAT-008: the Simple-mode (essential) set is exactly the six core groups")]
    public void CAT008_EssentialSet()
    {
        var essential = DspParameterCatalog.Groups.Where(g => g.Essential).Select(g => g.NameKey).ToHashSet();
        var expected = new HashSet<string>
        {
            "GrpGain", "GrpPitch", "GrpReverb", "GrpDistortion", "GrpRobot", "GrpChorus",
        };
        Assert.True(essential.SetEquals(expected),
            $"essential drift — only-actual: [{string.Join(",", essential.Except(expected))}], only-expected: [{string.Join(",", expected.Except(essential))}]");
    }
}
