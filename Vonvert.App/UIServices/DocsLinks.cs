// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <summary>
/// Web addresses of the guides shipped under <c>docs/</c>. Links point at the published
/// repository, not at the install folder: opening a raw markdown file in a browser gives a
/// wall of <c>#</c> and pipe characters, which is useless to the audience these guides are
/// written for. Only Chinese has translated guides, so every other language resolves to
/// the English page instead of to one that does not exist.
/// </summary>
public static class DocsLinks
{
    /// <summary>Default branch of the public repository; the docs are rendered from here.</summary>
    public const string BaseUrl = "https://github.com/ninesix-ai/Vonvert/blob/main/docs";

    public const string MonitorGuide       = "FULLSCREEN-MONITOR.md";
    public const string MonitorGuideZh     = "zh/FULLSCREEN-MONITOR.zh.md";
    public const string UserGuide          = "USER-GUIDE.md";
    public const string UserGuideZh        = "zh/USER-GUIDE.zh.md";

    /// <summary>Address of the fullscreen-monitor guide for a UI language.</summary>
    public static string MonitorGuideUrl(string? language) => Resolve(language, MonitorGuide, MonitorGuideZh);

    /// <summary>Address of the general user guide for a UI language.</summary>
    public static string UserGuideUrl(string? language) => Resolve(language, UserGuide, UserGuideZh);

    private static string Resolve(string? language, string englishFile, string chineseFile)
        => $"{BaseUrl}/{(language == "zh" ? chineseFile : englishFile)}";
}
