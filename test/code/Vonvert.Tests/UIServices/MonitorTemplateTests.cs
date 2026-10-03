// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// TP-001 ~ TP-009: the four one-click visual templates.
//
// Before this, getting the window into "the view I want while I stream" meant knowing that
// double-clicking a panel fills it, then picking the size, then doing it again next session.
// A template is that bundle behind one click. The invariants that matter: choosing the same
// template twice must not undo it (unlike a double-click), one template has to be the layout
// the window already shipped with so nobody's view changes on update, and a template must not
// reach out and change the palette or the text size - those are explicit choices in the same
// popup, and overriding them silently would make both controls lie about what they show.
public sealed class MonitorTemplateTests
{
    private static readonly MonitorTemplate[] All =
        { MonitorTemplate.Diagnose, MonitorTemplate.Stream, MonitorTemplate.Loudness, MonitorTemplate.Teaching };

    [Theory(DisplayName = "TP-001: every template is a legal layout - at most one panel filled")]
    [InlineData(MonitorTemplate.Diagnose)]
    [InlineData(MonitorTemplate.Stream)]
    [InlineData(MonitorTemplate.Loudness)]
    [InlineData(MonitorTemplate.Teaching)]
    [InlineData((MonitorTemplate)77)]
    public void TP001_TemplateLayoutIsLegal(MonitorTemplate template)
    {
        var layout = MonitorTemplates.For(template);
        if (layout.Maximized is { } pane)
            Assert.Contains(pane, System.Enum.GetValues<MonitorPane>());
    }

    [Fact(DisplayName = "TP-002: the default template is exactly the layout the window shipped with")]
    public void TP002_DefaultIsTheShippedLayout()
    {
        var layout = MonitorTemplates.For(MonitorTemplate.Diagnose);
        Assert.Null(layout.Maximized);                          // three panels, nothing filled
        Assert.True(layout.ShowPresetOverlay);                  // the voice name still fades in
        Assert.Equal(MonitorTemplates.DefaultCurveStrip, layout.CurveStripHeight);
    }

    [Fact(DisplayName = "TP-003: the four templates are four different views, not one with a toggle")]
    public void TP003_TemplatesAreDistinct()
    {
        // A template that only differs by one flag is not worth a button, so the whole
        // bundle has to be distinct per entry.
        Assert.Equal(4, All.Select(t => MonitorTemplates.For(t)).Distinct().Count());

        Assert.Equal(MonitorPane.Waterfall, MonitorTemplates.For(MonitorTemplate.Stream).Maximized);
        Assert.Equal(MonitorPane.Loudness, MonitorTemplates.For(MonitorTemplate.Loudness).Maximized);

        // Watching numbers does not need the voice name flashing over the meter; the two
        // views a streamer puts on air keep it.
        Assert.False(MonitorTemplates.For(MonitorTemplate.Loudness).ShowPresetOverlay);
        Assert.True(MonitorTemplates.For(MonitorTemplate.Stream).ShowPresetOverlay);

        // Pitch teaching is the one case that wants the curve large rather than the
        // spectrogram, and that is the difference between it and the streaming view.
        Assert.True(MonitorTemplates.For(MonitorTemplate.Teaching).CurveStripHeight
                    >= MonitorTemplates.DefaultCurveStrip * 1.5,
            "the teaching template must give the pitch curve real height, or it is just the streaming view");
    }

    [Fact(DisplayName = "TP-004: keys 1-4 pick the views in the order they are offered")]
    public void TP004_DigitsFollowTheOfferedOrder()
    {
        // There is one ordering fact here, not two: the digit a user presses and the row they
        // read in the popup must be the same list. Asserting it against the table would let the
        // buttons be re-ordered while the shortcuts quietly kept their old meaning.
        var mapped = new[] { Key.D1, Key.D2, Key.D3, Key.D4 }
            .Select(k => MonitorKeys.Map(k, ModifierKeys.None)!.Value)
            .ToList();
        var expected = MonitorTemplates.Order.Select(template => template switch
        {
            MonitorTemplate.Diagnose => MonitorKeyAction.TemplateDiagnose,
            MonitorTemplate.Stream => MonitorKeyAction.TemplateStream,
            MonitorTemplate.Loudness => MonitorKeyAction.TemplateLoudness,
            _ => MonitorKeyAction.TemplateTeaching,
        }).ToList();
        Assert.Equal(expected, mapped);
        Assert.Equal(4, MonitorTemplates.Order.Distinct().Count());
    }

    [Fact(DisplayName = "TP-005: setting the same template twice is idempotent, unlike a double-click")]
    public void TP005_SetMaximizedDoesNotFlip()
    {
        var layout = new PaneLayoutModel();
        layout.SetMaximized(MonitorPane.Loudness);
        layout.SetMaximized(MonitorPane.Loudness);
        Assert.Equal(MonitorPane.Loudness, layout.Maximized);   // Toggle would have undone it

        layout.SetMaximized(null);
        layout.SetMaximized(null);
        Assert.Null(layout.Maximized);
        Assert.Empty(layout.Collapsed());
    }

    [Theory(DisplayName = "TP-006: every template is named in every shipped language")]
    [MemberData(nameof(Languages))]
    public void TP006_NamesExistEverywhere(string lang)
    {
        var ui = ReadUiSection(lang);
        var names = All.Select(MonitorTemplates.NameKeyFor).ToArray();
        Assert.Equal(4, names.Distinct().Count());

        var missing = names.Append(MonitorTemplates.SectionKey).Where(k => !ui.Contains(k));
        Assert.True(!missing.Any(), $"{lang}: missing template copy [{string.Join(", ", missing)}]");
    }

    [Theory(DisplayName = "TP-006b: every language keeps the 1-4 hint in the views heading")]
    [MemberData(nameof(Languages))]
    public void TP006b_HeadingKeepsTheDigits(string lang)
    {
        // The heading is the only place the keyboard route is written down next to the buttons,
        // so it must survive translation with its numbers - the same rule KB-006 holds for the
        // shortcut line.
        var value = ReadUiValues(lang)[MonitorTemplates.SectionKey];
        Assert.True(value.Contains("1") && value.Contains("4"),
            $"{lang}: {MonitorTemplates.SectionKey} dropped the digit hint -> {value}");
    }

    [Fact(DisplayName = "TP-007: keys 1-4 pick a template, and only without a modifier")]
    public void TP007_DigitShortcuts()
    {
        // Plain digits, not Ctrl+1..4: the settings screen only lets a user bind a modifier
        // combination or an F-key globally, so a bare digit can never collide with their own
        // hotkey - and it matches the bare-letter family already shipped in this window.
        Assert.Equal(MonitorKeyAction.TemplateDiagnose, MonitorKeys.Map(Key.D1, ModifierKeys.None));
        Assert.Equal(MonitorKeyAction.TemplateStream, MonitorKeys.Map(Key.D2, ModifierKeys.None));
        Assert.Equal(MonitorKeyAction.TemplateLoudness, MonitorKeys.Map(Key.D3, ModifierKeys.None));
        Assert.Equal(MonitorKeyAction.TemplateTeaching, MonitorKeys.Map(Key.D4, ModifierKeys.None));

        Assert.Null(MonitorKeys.Map(Key.D1, ModifierKeys.Control));
        Assert.Null(MonitorKeys.Map(Key.D5, ModifierKeys.None));
    }

    public static IEnumerable<object[]> Languages()
        => LocalizationManager.SupportedLanguages.Select(code => new object[] { code });

    // ── source-level gates ──

    [Fact(DisplayName = "TP-008: the popup lists the views in the offered order, wired and named")]
    public void TP008_PopupMatchesTheOfferedOrder()
    {
        // Two ways this feature can be dead weight: a button with no Click (it looks live and
        // does nothing), and a list re-ordered under digits that still pick by position.
        var xaml = MonitorXaml;
        var found = System.Text.RegularExpressions.Regex
            .Matches(xaml, @"x:Name=""Template(\w+?)Btn""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(4, found.Count);
        var expected = MonitorTemplates.Order.Select(t => t switch
        {
            MonitorTemplate.Diagnose => "Diagnose",
            MonitorTemplate.Stream => "Stream",
            MonitorTemplate.Loudness => "Loudness",
            _ => "Teaching",
        }).ToList();
        Assert.Equal(expected, found);

        foreach (var name in found)
        {
            var button = System.Text.RegularExpressions.Regex.Match(
                xaml, @"<Button x:Name=""Template" + name + @"Btn""[\s\S]*?/>|<Button x:Name=""Template" + name + @"Btn""[\s\S]*?</Button>");
            Assert.True(button.Success, $"Template{name}Btn is not a complete element");
            Assert.True(button.Value.Contains($"Click=\"Template{name}_Click\""),
                $"Template{name}Btn has no handler - it would look like a button and do nothing");
            Assert.True(button.Value.Contains("AutomationProperties.Name="),
                $"Template{name}Btn is unnamed for a screen reader");
        }
    }

    [Fact(DisplayName = "TP-009: the window restores and saves the chosen view, so it survives a reopen")]
    public void TP009_ViewIsRemembered()
    {
        var code = MonitorCode;
        Assert.Contains("ApplyTemplate(_prefs.Template, save: false)", code);
        Assert.Contains("_prefs.Template = _template", code);
        // A manual layout change must clear the marker, or the popup claims a view the screen
        // is not showing.
        Assert.Contains("_template = null;", code);
    }

    private static string MonitorXaml => ReadFromRepo("Vonvert.App/Windows/FullscreenVisualizationWindow.xaml");
    private static string MonitorCode => ReadFromRepo("Vonvert.App/Windows/FullscreenVisualizationWindow.xaml.cs");

    private static string ReadFromRepo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

    private static Dictionary<string, string> ReadUiValues(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var map = new Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }

    private static HashSet<string> ReadUiSection(string lang)
    {
        var asm = typeof(LocalizationManager).Assembly;
        using var stream = asm.GetManifestResourceStream($"Vonvert.App.Translations.{lang}.json")
            ?? throw new System.InvalidOperationException($"embedded {lang}.json not found");
        using var doc = JsonDocument.Parse(stream);
        var set = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("ui", out var ui))
            foreach (var prop in ui.EnumerateObject())
                set.Add(prop.Name);
        return set;
    }
}
