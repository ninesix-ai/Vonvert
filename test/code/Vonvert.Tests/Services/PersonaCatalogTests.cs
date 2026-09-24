// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System.Linq;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Engine.Services;
using Xunit;

namespace Vonvert.Tests.Services;

public class PersonaCatalogTests
{
    [Fact]
    public void Catalog_Has_Eight_Personas_In_Document_Order()
        => Assert.Equal(8, PersonaCatalog.All.Count);

    [Fact]
    public void Groups_Cover_All_Personas_Exactly_Once()
    {
        var union = PersonaCatalog.Groups.SelectMany(PersonaCatalog.GetGroupMembers).ToList();
        Assert.Equal(8, union.Count);
        Assert.Equal(union.Count, union.Distinct().Count());
    }

    [Fact]
    public void TryParse_Is_Case_Insensitive_And_Rejects_Unknown()
    {
        Assert.True(PersonaCatalog.TryParse("gamer", out var g));
        Assert.Equal(PersonaType.Gamer, g);
        Assert.False(PersonaCatalog.TryParse("nope", out _));
        Assert.False(PersonaCatalog.TryParse(null, out _));
    }

    [Fact]
    public void Every_RecommendedPreset_Exists_As_BuiltIn()
    {
        foreach (var d in PersonaCatalog.All)
            foreach (var id in d.RecommendedPresetIds)
                Assert.True(BuiltInPresets.AllNames.Contains(id),
                    $"{d.Type} recommends unknown preset '{id}'");
    }

    [Fact]
    public void Get_Returns_Descriptor_With_NonEmpty_NameKey()
        => Assert.DoesNotContain(PersonaCatalog.All, d => string.IsNullOrWhiteSpace(d.NameKey));
}
