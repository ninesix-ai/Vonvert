// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guard for the update-check retry defect.
// HttpRetryPolicy documents that its send callback must build a FRESH
// HttpRequestMessage per attempt; the checker used to capture one request
// outside the callback, so the second attempt threw and the retry policy
// was effectively dead.  This test drives two transient 5xx responses and
// asserts three distinct request instances were sent before success.

namespace Vonvert.Tests.UI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vonvert.App.UIServices;
using Xunit;

public sealed class UpdateCheckerRetryTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _handler;
        public List<HttpRequestMessage> Seen { get; } = new();

        public StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            return Task.FromResult(_handler(request, Seen.Count));
        }
    }

    private static string MakeReleasesJson(params (string tag, bool prerelease)[] releases)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var (tag, prerelease) in releases)
            {
                writer.WriteStartObject();
                writer.WriteString("tag_name", tag);
                writer.WriteBoolean("prerelease", prerelease);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // ── UC-R01: fresh request per retry attempt ──────────────────────

    [Fact(DisplayName = "UC-R01: UpdateChecker — retry attempts each send a FRESH HttpRequestMessage")]
    public async Task CheckAsync_TransientServerErrors_RetriesWithFreshRequestEachAttempt()
    {
        var json = MakeReleasesJson(("v99.0.0", false));
        var handler = new StubHandler((req, attempt) => attempt < 3
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var checker = new UpdateChecker(client, maxAttempts: 3);

        await checker.CheckAsync();

        Assert.Equal(3, handler.Seen.Count);        // two 5xx retried, third attempt succeeded
        Assert.NotSame(handler.Seen[0], handler.Seen[1]);  // each attempt built a NEW request
        Assert.NotSame(handler.Seen[1], handler.Seen[2]);
        Assert.Equal("v99.0.0", checker.LatestTag);
    }
}
