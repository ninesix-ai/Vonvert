// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vonvert.App.UIServices;
using Vonvert.Engine.AudioEngine;
using Xunit;

// The soundboard used to explain its live/audition split through a 4-second toast, which
// is both too brief to read and useless for the ongoing question "is anyone else hearing
// me right now?". The replacement is a permanent status bar driven by a UI-free policy;
// these guards pin the state machine, the colour/text derivation, and the localisation
// contract that lets the bar be the single source of that answer.
public sealed class SoundboardStatusPolicyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static IReadOnlyDictionary<string, string> UiSection(string lang)
    {
        var path = Path.Combine(RepoRoot, "Vonvert.App", "Translations", $"{lang}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var map = new Dictionary<string, string>();
        foreach (var p in doc.RootElement.GetProperty("ui").EnumerateObject())
            map[p.Name] = p.Value.GetString()!;
        return map;
    }

    // ── state machine ──

    [Fact(DisplayName = "SBST-001: live mode with a running engine reads as an active broadcast")]
    public void Resolve_LiveAndRunning_IsBroadcast()
        => Assert.Equal(SoundboardStatusPolicy.Kind.LiveBroadcast,
                        SoundboardStatusPolicy.Resolve(liveMode: true, engineActive: true));

    [Fact(DisplayName = "SBST-002: a stopped engine outranks the broadcast wording (nothing is audible)")]
    public void Resolve_LiveAndStopped_OutsRanksBroadcast()
        => Assert.Equal(SoundboardStatusPolicy.Kind.LiveEngineStopped,
                        SoundboardStatusPolicy.Resolve(liveMode: true, engineActive: false));

    [Fact(DisplayName = "SBST-003: audition never surfaces a warning, whatever the engine does")]
    public void Resolve_Audition_IsAlwaysLocalOnly()
    {
        Assert.Equal(SoundboardStatusPolicy.Kind.AuditionLocalOnly,
                     SoundboardStatusPolicy.Resolve(liveMode: false, engineActive: true));
        // Audition owns an independent local output channel, so a stopped engine is still
        // not a problem worth warning about - the user hears their pads either way.
        Assert.Equal(SoundboardStatusPolicy.Kind.AuditionLocalOnly,
                     SoundboardStatusPolicy.Resolve(liveMode: false, engineActive: false));
    }

    [Fact(DisplayName = "SBST-004: the policy consumes EngineStatus directly and treats every non-Active state as stopped")]
    public void ForEngineStatus_OnlyActiveCountsAsRunning()
    {
        Assert.Equal(SoundboardStatusPolicy.Kind.LiveBroadcast,
                     SoundboardStatusPolicy.For(true, EngineStatus.Active, out _));
        // The engine only ever carries sound while Active, so Idle and Faulted must both
        // fall back to "nobody hears anything" rather than implying a live broadcast.
        foreach (var stopped in new[] { EngineStatus.Idle, EngineStatus.Faulted })
            Assert.Equal(SoundboardStatusPolicy.Kind.LiveEngineStopped,
                         SoundboardStatusPolicy.For(true, stopped, out _));
    }

    // ── derivation: text token + colour token ──

    [Fact(DisplayName = "SBST-005: every kind derives its text and colour tokens, and the wording matches the semantics")]
    public void Describe_EveryKind_HasCoherentTextAndColour()
    {
        var en = UiSection("en");
        var zh = UiSection("zh");

        foreach (var kind in Enum.GetValues<SoundboardStatusPolicy.Kind>())
        {
            var text = SoundboardStatusPolicy.Describe(kind);
            Assert.False(string.IsNullOrWhiteSpace(text.TextKey), $"{kind} has no text key");
            Assert.False(string.IsNullOrWhiteSpace(text.AccentToken), $"{kind} has no colour token");
            Assert.True(en.ContainsKey(text.TextKey), $"en.json is missing '{text.TextKey}'");
            Assert.True(zh.ContainsKey(text.TextKey), $"zh.json is missing '{text.TextKey}'");
        }

        // The audition line is the reassuring one: green, and phrased so the reader learns
        // others are excluded. A warning hue on it would be a false alarm.
        var audition = SoundboardStatusPolicy.Describe(SoundboardStatusPolicy.Kind.AuditionLocalOnly);
        Assert.Equal("PowerOn", audition.AccentToken);
        Assert.Contains("cannot hear", en[audition.TextKey], StringComparison.OrdinalIgnoreCase);

        foreach (var warning in new[] { SoundboardStatusPolicy.Kind.LiveBroadcast,
                                        SoundboardStatusPolicy.Kind.LiveEngineStopped })
        {
            var text = SoundboardStatusPolicy.Describe(warning);
            Assert.Equal("Warning", text.AccentToken);
            Assert.DoesNotContain("cannot hear", en[text.TextKey], StringComparison.OrdinalIgnoreCase);
        }

        // "Live is selected but stopped" must say nobody hears anything, otherwise the
        // user keeps believing the broadcast is live.
        var stopped = SoundboardStatusPolicy.Describe(SoundboardStatusPolicy.Kind.LiveEngineStopped);
        Assert.Contains("nobody", en[stopped.TextKey], StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SBST-006: the CTA stays its own key and is not baked into any status sentence")]
    public void CtaKey_IsSeparateFromDescriptiveStatusText()
    {
        var en = UiSection("en");
        const string cta = "SoundboardSwitchToAudition";
        Assert.True(en.ContainsKey(cta), $"'{cta}' must exist as its own key");
        Assert.DoesNotContain(":", en[cta]);           // a CTA is a label, not a sentence
        Assert.True(en[cta].Length < 30, "a CTA must stay short enough to fit a chip");

        // Status sentences may mention switching, but the actionable label must not be a
        // substring the view has to parse out of prose.
        foreach (var kind in Enum.GetValues<SoundboardStatusPolicy.Kind>())
        {
            var textKey = SoundboardStatusPolicy.Describe(kind).TextKey;
            Assert.NotEqual(cta, textKey);
            Assert.DoesNotContain(en[cta], en[textKey], StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "SBST-007: the old toast and the old engine hint are fully retired (no consumer, no key, no string)")]
    public void RetiredKeys_HaveNoLingeringReferences()
    {
        var en = UiSection("en");
        var zh = UiSection("zh");
        foreach (var retired in new[] { "SoundboardLiveModeNotice", "SoundboardEngineHint" })
        {
            Assert.False(en.ContainsKey(retired), $"en.json still carries retired key '{retired}'");
            Assert.False(zh.ContainsKey(retired), $"zh.json still carries retired key '{retired}'");
        }

        // A dead property or a stale binding would keep the key alive anywhere in the app;
        // scan every source file rather than a hand-picked list so a reference cannot hide
        // in a shell we forgot about (MainWindow, dialogs, ...).
        var appDir = Path.Combine(RepoRoot, "Vonvert.App");
        var offenders = Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(appDir, "*.xaml", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("SoundboardLiveModeNotice") || text.Contains("SoundboardEngineHint");
            })
            .Select(f => Path.GetRelativePath(appDir, f))
            .ToList();
        Assert.Empty(offenders);

        var view = File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Views", "SoundboardViewControl.xaml.cs"));
        Assert.DoesNotContain("SoundboardLiveModeNotice", view);
    }

    [Fact(DisplayName = "SBST-008: the status bar is permanent, so it must never be re-gated by an ad-hoc Visibility check")]
    public void StatusBar_IsNotConditionallyCollapsed()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Views", "SoundboardViewControl.xaml"));

        // Locate the status bar element and assert it ships visible: a Collapsed default is
        // how the per-context visibility drift used to start.
        var match = Regex.Match(xaml, "<Border\\s+x:Name=\"SbStatusBar\"(?<attrs>[^>]*)>",
                                RegexOptions.Singleline);
        Assert.True(match.Success, "SbStatusBar border not found in SoundboardViewControl.xaml");
        Assert.DoesNotContain("Visibility=\"Collapsed\"", match.Groups["attrs"].Value);

        var cs = File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Views", "SoundboardViewControl.xaml.cs"));
        Assert.DoesNotContain("SbStatusBar.Visibility", cs);
        Assert.DoesNotContain("EngineHint.Visibility", cs);
    }

    [Fact(DisplayName = "SBST-009: mode chips stop sharing the decorative category style and use semantic hues")]
    public void ModeChips_UseSemanticStyleNotCategoryChip()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "Views", "SoundboardViewControl.xaml"));

        // The aud/live radio buttons must be on a mode-specific style...
        foreach (var name in new[] { "ModeAuditionChip", "ModeLiveChip" })
        {
            var m = Regex.Match(xaml, $"x:Name=\"{name}\"[^>]*Style=\"(?<style>[^\"]+)\"", RegexOptions.Singleline);
            Assert.True(m.Success, $"{name} not found");
            Assert.Equal("{StaticResource SbModeChip}", m.Groups["style"].Value);
        }

        // ...whose checked visuals reference the semantic brushes rather than Accent.
        var style = Regex.Match(xaml, "<Style\\s+x:Key=\"SbModeChip\"(?<body>.*?)</Style>",
                                RegexOptions.Singleline).Groups["body"].Value;
        Assert.Contains("Warning", style);
        Assert.Contains("PowerOn", style);
        Assert.Contains("SbModeLive", style);       // the live chip keys off its own selector
        Assert.Contains("SbModeAudition", style);
    }
}
