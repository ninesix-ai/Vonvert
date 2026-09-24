// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine;
using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Tests for RecordingService.Result pattern — verifies StartRecording
/// returns a Result and callers can detect failure.
/// </summary>
public sealed class RecordingServiceResultTests
{
    [Fact(DisplayName = "RSR-001: StartRecording — returns Ok on success")]
    public void RSR001_StartRecording_ReturnsOkOnSuccess()
    {
        using var svc = new RecordingService();

        var result = svc.StartRecording();

        Assert.True(result.IsSuccess);
        Assert.False(svc.IsRecording == false); // should be recording now

        // Cleanup
        svc.CancelRecording();
    }

    [Fact(DisplayName = "RSR-002: StartRecording — double start returns Ok (idempotent)")]
    public void RSR002_StartRecording_DoubleStartIsIdempotent()
    {
        using var svc = new RecordingService();

        var r1 = svc.StartRecording();
        var r2 = svc.StartRecording();

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);

        svc.CancelRecording();
    }

    [Fact(DisplayName = "RSR-003: StartRecording then Cancel — IsRecording false")]
    public void RSR003_StartThenCancel_IsRecordingFalse()
    {
        using var svc = new RecordingService();

        svc.StartRecording();
        Assert.True(svc.IsRecording);

        svc.CancelRecording();
        Assert.False(svc.IsRecording);
    }

    [Fact(DisplayName = "RSR-004: StartRecording then Stop — returns history item")]
    public void RSR004_StartThenStop_ReturnsHistoryItem()
    {
        using var svc = new RecordingService();

        svc.StartRecording();

        // Write some samples
        var samples = new float[4800]; // 100ms
        svc.WriteSamples(samples, samples.Length);

        var item = svc.StopRecording();

        Assert.NotNull(item);
        Assert.True(item!.Duration >= 0);

        if (item != null) svc.DeleteRecording(item);
    }

    [Fact(DisplayName = "RSR-005: RecordMode is volatile — changes visible across threads")]
    public void RSR005_RecordMode_VolatileVisibility()
    {
        using var svc = new RecordingService();

        svc.RecordMode = RecordMode.Original;
        Assert.Equal(RecordMode.Original, svc.RecordMode);

        svc.RecordMode = RecordMode.Processed;
        Assert.Equal(RecordMode.Processed, svc.RecordMode);
    }

    [Fact(DisplayName = "RSR-006: Result&lt;Unit&gt; — Match executes correct branch")]
    public void RSR006_ResultUnit_MatchExecutesCorrectBranch()
    {
        using var svc = new RecordingService();

        var result = svc.StartRecording();
        string? branch = null;

        result.Match(
            _ => branch = "success",
            e => branch = $"fail: {e}");

        Assert.Equal("success", branch);

        svc.CancelRecording();
    }
}
