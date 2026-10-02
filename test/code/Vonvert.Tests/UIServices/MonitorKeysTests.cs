// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using System.Windows.Input;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// KB-001 ~ KB-006: the monitor's keyboard map.
//
// The window was mouse-only: a streamer who opened it by accident had to find the black
// rectangle, aim a double-click, or know about Esc without ever seeing a button. These
// tests keep the map complete, uncluttered, and in step with the shortcut line the help
// card shows.
public sealed class MonitorKeysTests
{
    [Fact(DisplayName = "KB-001: every advertised key maps to its own action, none doubled up")]
    public void KB001_AllKeysDistinct()
    {
        var keys = new[] { Key.Escape, Key.H, Key.T, Key.S, Key.R, Key.D, Key.W, Key.G };
        var actions = keys.Select(k => MonitorKeys.Map(k, ModifierKeys.None)).ToList();

        Assert.All(actions, a => Assert.NotNull(a));
        Assert.Equal(keys.Length, actions.Distinct().Count());
    }

    [Fact(DisplayName = "KB-002: Escape still closes, and F1 opens help for the person hunting for it")]
    public void KB002_ExpectedDefaultKeys()
    {
        Assert.Equal(MonitorKeyAction.Close, MonitorKeys.Map(Key.Escape, ModifierKeys.None));
        Assert.Equal(MonitorKeyAction.ToggleHelp, MonitorKeys.Map(Key.F1, ModifierKeys.None));
    }

    [Theory(DisplayName = "KB-003: a modified key belongs to the OS or the app, never swallowed here")]
    [InlineData(Key.S, ModifierKeys.Control)]
    [InlineData(Key.W, ModifierKeys.Alt)]
    [InlineData(Key.F4, ModifierKeys.Alt)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    public void KB003_ModifiedKeysIgnored(Key key, ModifierKeys mods)
        => Assert.Null(MonitorKeys.Map(key, mods));

    [Fact(DisplayName = "KB-004: the F9-F12 range stays free for the user's own global hotkeys")]
    public void KB004_GlobalHotkeyRangeUntouched()
    {
        foreach (var key in new[] { Key.F9, Key.F10, Key.F11, Key.F12 })
            Assert.Null(MonitorKeys.Map(key, ModifierKeys.None));
    }

    [Fact(DisplayName = "KB-005: the help line lists every bound letter, so documentation cannot drift")]
    public void KB005_ShortcutLineCoversTheMap()
    {
        var summary = MonitorKeys.ShortcutSummary;
        foreach (char letter in new[] { 'H', 'T', 'S', 'R', 'D', 'W', 'G' })
            Assert.Contains($"{letter} ", summary);
        Assert.Contains("Esc", summary);
    }

    [Theory(DisplayName = "KB-006: every language keeps the shortcut letters and Esc when it translates the words")]
    [MemberData(nameof(Languages))]
    public void KB006_LocalizedShortcutLineKeepsTheKeys(string lang)
    {
        var ui = ReadUiSection(lang);
        Assert.True(ui.ContainsKey("FsHelpKeys"), $"{lang} has no FsHelpKeys value");
        var value = ui["FsHelpKeys"];
        foreach (char letter in new[] { 'H', 'T', 'S', 'R', 'D', 'W', 'G' })
            Assert.Contains($"{letter} ", value);
        Assert.Contains("Esc", value);
    }

    public static System.Collections.Generic.IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    private static System.Collections.Generic.Dictionary<string, string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = System.Text.Json.JsonDocument.Parse(stream);
        var map = new System.Collections.Generic.Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }
}
