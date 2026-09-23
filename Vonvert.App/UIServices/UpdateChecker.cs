// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Vonvert.Engine;
using Vonvert.Engine.Services;

namespace Vonvert.App.UIServices;

/// <summary>
/// Periodically checks for major version announcements via a lightweight
/// announcement endpoint (GitHub Releases "latest" is no longer used to
/// comply with Microsoft Store policy — Store builds use Store updates).
///
/// Only fires <see cref="UpdateAvailable"/> when a new MAJOR version is
/// detected (e.g. 2.x → 3.x). Minor/patch updates are handled by the
/// Microsoft Store automatic update mechanism.
///
/// All network errors are silently ignored — this is best-effort only.
/// </summary>
public sealed class UpdateChecker
{
    // ── Configuration (from AppConfig) ─────────────────────────
    // Use the GitHub releases API only for major version announcements.
    // The Store handles minor/patch updates automatically.
    private string AnnouncementApi => AppConfig.Instance.Network.UpdateAnnouncementApi;
    private string AnnouncementPage => AppConfig.Instance.Network.UpdateAnnouncementPage;
    private TimeSpan CheckInterval => TimeSpan.FromHours(AppConfig.Instance.Network.UpdateCheckIntervalHours);

    // ── State ────────────────────────────────────────────────────
    private readonly DispatcherTimer _timer = new();
    private readonly HttpClient _http;
    private readonly HttpRetryPolicy _retry;

    /// <summary>Latest major version tag, or null if not yet checked / up-to-date.</summary>
    public string? LatestTag { get; private set; }

    /// <summary>Announcement page URL.</summary>
    public string ReleaseUrl => AnnouncementPage;

    /// <summary>Raised on the UI thread when a new major version is detected.</summary>
    public event Action<string, string>? UpdateAvailable;   // (latestTag, announcementUrl)

    /// <summary>Delay before retrying after a failed check.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    /// <summary>Current timer interval (observable for tests).</summary>
    internal TimeSpan CurrentTimerInterval => _timer.Interval;

    public UpdateChecker()
    {
        _http = VonvertHttpFactory.CreateDefault();
        _retry = new HttpRetryPolicy(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(500));
        _timer.Interval = CheckInterval;
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    /// <summary>Internal constructor for unit testing with a custom <see cref="HttpClient"/>.
    /// <paramref name="maxAttempts"/> defaults to 1 (single shot); pass 3 to exercise the retry path.</summary>
    internal UpdateChecker(HttpClient testHttp, int maxAttempts = 1)
    {
        _http = testHttp;
        _retry = new HttpRetryPolicy(maxAttempts: maxAttempts, initialDelay: TimeSpan.FromMilliseconds(1));
        _timer.Interval = CheckInterval;
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    /// <summary>
    /// Start periodic checking. Performs an immediate first check,
    /// then repeats every <see cref="CheckInterval"/>.
    /// </summary>
    public void Start()
    {
        _timer.Start();
        // Fire-and-forget the first check so we don't block the UI thread.
        _ = CheckAsync();
    }

    /// <summary>Stop periodic checking.</summary>
    public void Stop() => _timer.Stop();

    // ── Core logic ───────────────────────────────────────────────

    internal async Task CheckAsync()
    {
        try
        {
            // HttpRetryPolicy requires a FRESH HttpRequestMessage per attempt —
            // HttpClient refuses to re-send the same request instance, which used to
            // short-circuit the whole retry policy after the first transient failure.
            using var resp = await _retry.ExecuteAsync(SendAnnouncementRequestAsync);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            var remoteTag = ParseLatestStableTag(json);
            if (remoteTag == null) return;

            LatestTag = remoteTag;

            // Only notify for MAJOR version changes (e.g. 1.x → 2.x, 2.x → 3.x)
            // Minor/patch updates are handled by Microsoft Store.
            if (IsMajorNewer(remoteTag))
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    UpdateAvailable?.Invoke(remoteTag, AnnouncementPage));
            }

            // Success — restore the normal check interval.
            _timer.Interval = CheckInterval;
        }
        catch (Exception ex)
        {
            // On failure, shorten the next interval so we retry within 1 h
            // instead of waiting the full 24 h check interval.
            AppLog.Warning(ex, "UpdateChecker: failed to check for version announcements — retrying in {Retry}", RetryDelay);
            _timer.Interval = RetryDelay;
        }
    }

    /// <summary>Builds and sends a new announcement request for one retry attempt.
    /// HttpClient takes ownership of the request message once the send completes.</summary>
    private Task<HttpResponseMessage> SendAnnouncementRequestAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, AnnouncementApi);
        req.Headers.Add("Accept", "application/vnd.github+json");
        return _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    }

    /// <summary>
    /// Compare the remote tag against the running assembly version.
    /// Returns true only if the remote tag has a newer MAJOR version
    /// (e.g. running 1.x and remote is 2.x+).
    /// </summary>
    internal static bool IsMajorNewer(string remoteTag)
    {
        try
        {
            var remoteVer = ParseVersion(remoteTag);
            if (remoteVer == null) return false;

            var asm = Assembly.GetExecutingAssembly();
            var current = asm.GetName().Version;
            if (current == null) return false;

            // Only trigger for major version differences
            return remoteVer.Major > current.Major;
        }
        catch { return false; }
    }

    /// <summary>
    /// Parse a version tag like "v1.2.3" or "1.2.3" into a <see cref="Version"/>.
    /// </summary>
    internal static Version? ParseVersion(string tag)
    {
        var s = tag.TrimStart('v', 'V').Trim();
        // Handle pre-release suffixes like "1.2.3-beta"
        var dashIdx = s.IndexOf('-');
        if (dashIdx >= 0) s = s[..dashIdx];
        return Version.TryParse(s, out var v) ? v : null;
    }

    /// <summary>
    /// Parse a GitHub Releases JSON array and return the first non-prerelease
    /// tag_name, or null if no stable release is found.
    /// </summary>
    internal static string? ParseLatestStableTag(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
                continue;
            if (!release.TryGetProperty("tag_name", out var tagEl))
                continue;
            var tag = tagEl.GetString();
            if (string.IsNullOrWhiteSpace(tag)) continue;
            return tag;
        }

        return null;
    }
}
