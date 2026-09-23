// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Net.Http;

namespace Vonvert.Engine.Services;

/// <summary>
/// Shared HTTP infrastructure for all Vonvert network services.
/// Provides consistent User-Agent, timeout, and header defaults so each
/// service does not need to configure its own HttpClient from scratch.
///
/// Services that need special behaviour (certificate pinning, redirect
/// suppression) can call <see cref="CreateDefault"/> and then customise
/// the returned client, or build their own handler pipeline.
/// </summary>
public static class VonvertHttpFactory
{
    /// <summary>Canonical User-Agent sent by every Vonvert HTTP request.</summary>
    public const string UserAgent = "Vonvert/3.0";

    /// <summary>Default request timeout (matches AppConfig Network.HttpTimeoutSeconds).</summary>
    public static TimeSpan DefaultTimeout =>
        TimeSpan.FromSeconds(AppConfig.Instance.Network.HttpTimeoutSeconds);

    /// <summary>
    /// Create an <see cref="HttpClient"/> with the standard Vonvert headers
    /// (User-Agent, Accept) and the configured timeout.
    /// </summary>
    /// <param name="timeout">
    /// Optional override — when null, <see cref="DefaultTimeout"/> is used.
    /// </param>
    /// <param name="handler">
    /// Optional custom handler (e.g. one that disables auto-redirect).
    /// When null, a default <see cref="HttpClientHandler"/> is used.
    /// </param>
    public static HttpClient CreateDefault(
        TimeSpan? timeout = null,
        HttpMessageHandler? handler = null)
    {
        var client = handler != null
            ? new HttpClient(handler)
            : new HttpClient();

        client.Timeout = timeout ?? DefaultTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}
