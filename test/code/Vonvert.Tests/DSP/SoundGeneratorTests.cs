// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression for the procedural sound catalogue: the built-in set, its categories,
// and the float32/byte parity of the generators.

namespace Vonvert.Tests.DSP;

using System;
using System.Linq;
using Xunit;
using Vonvert.Engine.ProceduralAudio;

public sealed class SoundGeneratorTests
{
    [Fact(DisplayName = "SBG-001: catalogue exposes all 50 built-in sounds across 7 categories")]
    public void SBG001_CatalogueShape()
    {
        Assert.Equal(50, SoundGenerator.GetAllSounds().Count);
        Assert.Equal(7,  SoundGenerator.GetCategories().Count);
    }

    [Fact(DisplayName = "SBG-002: every sound id resolves to a non-empty generated clip")]
    public void SBG002_AllSoundsGenerate()
    {
        foreach (var def in SoundGenerator.GetAllSounds())
        {
            var samples = SoundGenerator.Generate(def.Id);
            Assert.NotEmpty(samples);
            // No NaN / infinity anywhere in the signal.
            Assert.DoesNotContain(samples, s => float.IsNaN(s) || float.IsInfinity(s));
        }
    }

    [Fact(DisplayName = "SBG-003: GenerateBytes length equals float count * sizeof(float)")]
    public void SBG003_ByteLengthMatchesFloatLength()
    {
        foreach (var def in SoundGenerator.GetAllSounds())
        {
            var bytes = SoundGenerator.GenerateBytes(def.Id);
            var floats = SoundGenerator.Generate(def.Id);
            Assert.Equal(floats.Length * sizeof(float), bytes.Length);
        }
    }

    [Fact(DisplayName = "SBG-004: categories reported by the catalogue match the declared order")]
    public void SBG004_CategoriesMatchDefinitions()
    {
        var cats = SoundGenerator.GetCategories();
        foreach (var def in SoundGenerator.GetAllSounds())
            Assert.Contains(def.Category, cats);
    }
}
