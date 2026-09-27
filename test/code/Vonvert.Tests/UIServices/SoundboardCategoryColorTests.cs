// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

// Pad silhouettes are tinted per category: emoji glyphs render monochrome in this layered
// window, so the hue is the only category cue on the pad. This guard ties the three places
// that must agree - the engine's category list, the Theme.xaml hue tokens, and the pad
// DataTriggers - so a renamed or newly added category can never silently ship colourless.
public sealed class SoundboardCategoryColorTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static string[] EngineCategories()
    {
        var src = Read("Vonvert.Engine", "ProceduralAudio", "SoundGenerator.cs");
        var line = Regex.Match(src, "CategoryOrder\\s*=\\s*\\[(?<items>[^\\]]*)\\]");
        Assert.True(line.Success, "CategoryOrder array not found in SoundGenerator.cs");
        return Regex.Matches(line.Groups["items"].Value, "\"([^\"]+)\"")
                    .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
    }

    [Fact(DisplayName = "SBCAT-001: engine category list is discovered (anchor sanity)")]
    public void CategoryAnchor_IsDiscovered()
        => Assert.Equal(
            new[] { "Drums", "Tones", "SFX", "Memes", "Music", "Retro", "Ambient" },
            EngineCategories());

    [Theory(DisplayName = "SBCAT-002: every category has a Theme.xaml hue token pair")]
    [MemberData(nameof(AllCategories))]
    public void EveryCategory_HasThemeTokens(string category)
    {
        // XAML is free-form about spacing, so compare on collapsed whitespace.
        var theme = Collapse(Read("Vonvert.App", "AppStyles", "Theme.xaml"));
        Assert.Contains($"<Color x:Key=\"C_IconCat{category}\"", theme);
        Assert.Contains(
            $"<SolidColorBrush x:Key=\"IconCat{category}\" Color=\"{{StaticResource C_IconCat{category}}}\"/>",
            theme);
    }

    private static string Collapse(string text)
        => Regex.Replace(text, "\\s+", " ");

    [Theory(DisplayName = "SBCAT-003: pad template tints the glyph for every category")]
    [MemberData(nameof(AllCategories))]
    public void EveryCategory_IsAppliedInPadTemplate(string category)
    {
        var view = Read("Vonvert.App", "Views", "SoundboardViewControl.xaml");
        Assert.Contains($"Value=\"{category}\"", view);
        Assert.Contains($"DynamicResource IconCat{category}", view);
    }

    public static TheoryData<string> AllCategories()
    {
        var data = new TheoryData<string>();
        foreach (var c in EngineCategories()) data.Add(c);
        return data;
    }
}
