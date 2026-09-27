// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Vonvert.App.UIServices;
using Xunit;

// Guard for the soundboard vector-glyph migration: every built-in sound id maps to a
// glyph resource that actually exists in Assets/Icons.xaml, category fallbacks and the
// user/unknown keys resolve, so a pad can never render as an empty Image.
public sealed class SoundboardIconMapTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string[] BuiltinIds()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.Engine", "ProceduralAudio", "SoundGenerator.cs"));
        return Regex.Matches(src, "new\\(\"([a-z0-9_]+)\",").Select(m => m.Groups[1].Value).ToArray();
    }

    private static string IconsXaml()
        => File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Assets", "Icons.xaml"));

    [Fact(DisplayName = "ICON-001: catalog is discovered and non-empty (regex anchor sanity)")]
    public void CatalogAnchor_Works()
        => Assert.Equal(50, BuiltinIds().Length);

    [Theory(DisplayName = "ICON-002: every built-in id resolves to a key defined in Icons.xaml")]
    [MemberData(nameof(AllBuiltinIds))]
    public void EveryBuiltin_ResolvesToDefinedGlyph(string id)
    {
        string key = SoundboardIconMap.Resolve(id, "Drums", isUserImported: false);
        Assert.Contains($"x:Key=\"{key}\"", IconsXaml());
    }

    public static TheoryData<string> AllBuiltinIds()
    {
        var data = new TheoryData<string>();
        foreach (var id in BuiltinIds()) data.Add(id);
        return data;
    }

    [Theory(DisplayName = "ICON-003: category fallbacks and special keys are all defined")]
    [InlineData("no_such_id", "Tones", "IcSbSine")]
    [InlineData("no_such_id", "SFX", "IcSbBolt")]
    [InlineData("no_such_id", "Ambient", "IcSbWave")]
    [InlineData("no_such_id", "Bogus", "IcSbUnknown")]
    [InlineData("some_user_file", "User", "IcSbUser")]
    public void Fallbacks_UseDefinedKeys(string id, string category, string expectedKey)
    {
        bool isUser = category == "User";
        Assert.Equal(expectedKey, SoundboardIconMap.Resolve(id, category, isUser));
        Assert.Contains($"x:Key=\"{expectedKey}\"", IconsXaml());
    }
}
