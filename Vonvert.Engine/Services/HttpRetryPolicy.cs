// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Net.Http;
using System.Net.Http.Headers;

namespace Vonvert.Engine.Services;

/// <summary>
/// Shared retry policy for external HTTP calls.
///
/// Rules:
///   - Retries transient failures: network errors (<see cref="HttpRequestException"/>),
///     timeouts (<see cref="TaskCanceledException"/> unless the caller cancelled),
///     HTTP 5xx, and HTTP 429 (rate-limited).
///   - Never retries client errors other than 429 (4xx) — retrying a bad request
///     is pointless and can amplify abuse.
///   - Exponential backoff: 1s / 2s / 4s by default (jittered), capped by
///     <see cref="MaxDelay"/>; honors Retry-After on 429/503 when supplied.
///   - Total attempts are bounded by <paramref name="maxAttempts"/> (default 3).
///
/// Thread-safe: one instance may be shared; the retry counter is atomic.
/// </summary>
public sealed class HttpRetryPolicy
{
    public const int DefaultMaxAttempts = 3;
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(4);

    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly Random _jitter = new();
    private int _retries;

    /// <summary>Total retries performed by this policy so far (observable for tests).</summary>
    public int TotalRetries => Volatile.Read(ref _retries);

    public HttpRetryPolicy(
        int? maxAttempts = null,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var cfg = AppConfig.Instance.Network;
        _maxAttempts = Math.Max(1, maxAttempts ?? cfg.RetryMaxAttempts);
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(cfg.RetryInitialDelaySeconds);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(cfg.RetryMaxDelaySeconds);
    }

    /// <summary>
    /// Execute an HTTP operation with retries and exponential backoff.
    /// <paramref name="send"/> must build a fresh request per call (HttpClient
    /// forbids re-sending a consumed request message); when it completes the
    /// caller owns the returned response and must dispose it.
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken ct = default)
    {
        TimeSpan? retryAfter = null;
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            Exception? error = null;
            try
            {
                response = await send().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                if (!IsRetryableException(error, ct) || attempt >= _maxAttempts)
                    throw error;
            }
            else if (response != null)
            {
                if (!IsRetryableResponse(response) || attempt >= _maxAttempts)
                    return response;
                retryAfter = ReadRetryAfter(response);
                response.Dispose();
            }
            else
            {
                return null!; // unreachable: send() either returns or throws
            }

            Interlocked.Increment(ref _retries);
            await BackoffAsync(attempt, retryAfter, ct).ConfigureAwait(false);
        }
    }

    /// <summary>True when the exception is transient (network error / timeout).</summary>
    public static bool IsRetryableException(Exception ex, CancellationToken ct = default)
        => !ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException;

    /// <summary>True when the status code is transient (5xx, 429, or 403 with rate-limit headers).</summary>
    public static bool IsRetryableResponse(HttpResponseMessage response)
    {
        int code = (int)response.StatusCode;
        if (code == 429) return true;
        if (code >= 500 && code <= 599) return true;

        // GitHub secondary rate limit returns 403 with a Retry-After header
        // or an x-ratelimit-remaining: 0 header.  Only retry 403 when these
        // signals are present — a plain 403 (Forbidden) is not transient.
        if (code == 403)
        {
            if (response.Headers.RetryAfter != null) return true;
            if (response.Headers.TryGetValues("x-ratelimit-remaining", out var vals))
            {
                foreach (var v in vals)
                    if (v.Trim() == "0") return true;
            }
        }

        return false;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null) return null;
        if (retryAfter.Delta.HasValue) return retryAfter.Delta.Value;
        if (retryAfter.Date.HasValue)
        {
            var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }
        return null;
    }

    private async Task BackoffAsync(int attempt, TimeSpan? retryAfter, CancellationToken ct)
    {
        // Exponential: initial * 2^(attempt-1), capped at MaxDelay, plus jitter.
        long ticks = _initialDelay.Ticks * (1L << Math.Min(attempt - 1, 10));
        var baseDelay = TimeSpan.FromTicks(Math.Min(ticks, _maxDelay.Ticks));
        double jitterMs = Math.Min(250, baseDelay.TotalMilliseconds * 0.25);
        var delay = baseDelay + TimeSpan.FromMilliseconds(_jitter.NextDouble() * jitterMs);

        if (retryAfter.HasValue && retryAfter.Value > delay)
        {
            // Honor Retry-After but never wait absurdly long (cap at 4x MaxDelay).
            var cap = TimeSpan.FromTicks(_maxDelay.Ticks * 4);
            delay = retryAfter.Value > cap ? cap : retryAfter.Value;
        }

        try { await Task.Delay(delay, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }
}
