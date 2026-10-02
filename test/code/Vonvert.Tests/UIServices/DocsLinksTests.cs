// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using System.Linq;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// DL-001 ~ DL-005: the help links the UI opens in the browser.
//
// A link that points at a doc file that was renamed, or at a translated page that was
// never written, is invisible until somebody clicks it - which is exactly when it stops
// being a help link. These tests bind the URLs to the files that exist in the repository.
public sealed class DocsLinksTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }

    [Theory(DisplayName = "DL-001: Chinese resolves to the translated guides")]
    [InlineData("zh")]
    public void DL001_ChineseUsesZhSiblings(string lang)
    {
        Assert.EndsWith("/docs/zh/FULLSCREEN-MONITOR.zh.md", DocsLinks.MonitorGuideUrl(lang));
        Assert.EndsWith("/docs/zh/USER-GUIDE.zh.md", DocsLinks.UserGuideUrl(lang));
    }

    [Theory(DisplayName = "DL-002: English, unknown and missing language all resolve to the English guide")]
    [InlineData("en")]
    [InlineData("sv")]        // not a shipped language: must not build a path to nothing
    [InlineData("zh-TW")]     // must not invent a regional variant of the zh file
    [InlineData("")]
    public void DL002_FallbackIsEnglish(string lang)
    {
        Assert.EndsWith("/docs/FULLSCREEN-MONITOR.md", DocsLinks.MonitorGuideUrl(lang));
        Assert.EndsWith("/docs/USER-GUIDE.md", DocsLinks.UserGuideUrl(lang));
    }

    [Fact(DisplayName = "DL-003: every link is an absolute https URL on the default branch, never a local path")]
    public void DL003_LinksAreWebUrls()
    {
        foreach (var lang in new[] { "en", "zh", "de", "ja" })
        {
            foreach (var url in new[] { DocsLinks.MonitorGuideUrl(lang), DocsLinks.UserGuideUrl(lang) })
            {
                Assert.StartsWith("https://github.com/", url);
                Assert.Contains("/blob/main/docs/", url);
                Assert.False(url.Contains(":\\") || url.Contains("file://"),
                    $"a UI help link must open a rendered web page, not a local file: {url}");
            }
        }
    }

    [Fact(DisplayName = "DL-004: the files behind those links exist in this repository")]
    public void DL004_LinkedFilesExist()
    {
        var docs = Path.Combine(RepoRoot, "docs");
        foreach (var relative in new[]
        {
            DocsLinks.MonitorGuide, DocsLinks.MonitorGuideZh,
            DocsLinks.UserGuide,    DocsLinks.UserGuideZh,
        })
            Assert.True(File.Exists(Path.Combine(docs, relative.Replace('/', Path.DirectorySeparatorChar))),
                $"{relative} is linked from the app but is missing from docs/");
    }

    [Fact(DisplayName = "DL-005: only zh gets translated guides, so no other language links to a 404")]
    public void DL005_UntranslatedLanguagesDoNotGetZhPaths()
    {
        var offenders = LocalizationManager.SupportedLanguages
            .Where(l => l != "zh")
            .Where(l => DocsLinks.MonitorGuideUrl(l).Contains("zh/"))
            .ToList();
        Assert.True(offenders.Count == 0,
            $"these languages have no translated guide but link to one: {string.Join(", ", offenders)}");
    }

    [Fact(DisplayName = "DL-006: the monitor guide itself links back to the other guides by relative path")]
    public void DL006_MonitorGuideCrossLinksExist()
    {
        var en = Path.Combine(RepoRoot, "docs", "FULLSCREEN-MONITOR.md");
        var zh = Path.Combine(RepoRoot, "docs", "zh", "FULLSCREEN-MONITOR.zh.md");
        var enText = File.ReadAllText(en);
        var zhText = File.ReadAllText(zh);

        Assert.Contains("USER-GUIDE.md", enText);
        Assert.Contains("VB-CABLE.md", enText);
        Assert.Contains("(zh/FULLSCREEN-MONITOR.zh.md)", enText);      // language switch
        Assert.Contains("(../FULLSCREEN-MONITOR.md)", zhText);         // language switch back
        Assert.Contains("USER-GUIDE.zh.md", zhText);
        Assert.Contains("VB-CABLE.zh.md", zhText);
    }
}
