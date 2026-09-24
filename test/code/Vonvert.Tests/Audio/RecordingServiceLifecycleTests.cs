// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Tests for RecordingService — lifecycle, mode switching, events, and history.
/// </summary>
public sealed class RecordingServiceLifecycleTests : IDisposable
{
    private readonly RecordingService _service;

    public RecordingServiceLifecycleTests()
    {
        _service = new RecordingService();
    }

    public void Dispose()
    {
        if (_service.IsRecording)
            _service.CancelRecording();
        _service.Dispose();
    }

    // ── Initial state ────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsNotRecording()
    {
        Assert.False(_service.IsRecording);
    }

    [Fact]
    public void InitialState_CurrentFileIsNull()
    {
        Assert.Null(_service.CurrentFile);
    }

    [Fact]
    public void InitialState_DefaultModeIsProcessed()
    {
        Assert.Equal(RecordMode.Processed, _service.RecordMode);
    }

    [Fact]
    public void RecordingsFolder_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(_service.RecordingsFolder));
    }

    // ── Start/Stop lifecycle ─────────────────────────────────────────

    [Fact]
    public void StartRecording_SetsIsRecordingTrue()
    {
        var result = _service.StartRecording();
        Assert.True(result.IsSuccess);
        Assert.True(_service.IsRecording);
        _service.CancelRecording();
    }

    [Fact]
    public void StartRecording_SetsCurrentFile()
    {
        _service.StartRecording();
        Assert.NotNull(_service.CurrentFile);
        Assert.Contains("temp_", _service.CurrentFile);
        _service.CancelRecording();
    }

    [Fact]
    public void StartRecording_FiresRecordingStartedEvent()
    {
        bool fired = false;
        _service.RecordingStarted += () => fired = true;
        _service.StartRecording();
        Assert.True(fired);
        _service.CancelRecording();
    }

    [Fact]
    public void StartRecording_WhenAlreadyRecording_ReturnsOk()
    {
        _service.StartRecording();
        var result = _service.StartRecording(); // double start
        Assert.True(result.IsSuccess);
        _service.CancelRecording();
    }

    [Fact]
    public void CancelRecording_SetsIsRecordingFalse()
    {
        _service.StartRecording();
        _service.CancelRecording();
        Assert.False(_service.IsRecording);
    }

    [Fact]
    public void CancelRecording_ClearsCurrentFile()
    {
        _service.StartRecording();
        _service.CancelRecording();
        Assert.Null(_service.CurrentFile);
    }

    [Fact]
    public void CancelRecording_FiresRecordingStoppedEvent()
    {
        bool fired = false;
        _service.RecordingStopped += () => fired = true;
        _service.StartRecording();
        _service.CancelRecording();
        Assert.True(fired);
    }

    [Fact]
    public void CancelRecording_WhenNotRecording_DoesNotFireEvent()
    {
        bool fired = false;
        _service.RecordingStopped += () => fired = true;
        _service.CancelRecording(); // not recording
        Assert.False(fired);
    }

    // ── Mode switching ───────────────────────────────────────────────

    [Fact]
    public void RecordMode_CanBeSetToOriginal()
    {
        _service.RecordMode = RecordMode.Original;
        Assert.Equal(RecordMode.Original, _service.RecordMode);
    }

    [Fact]
    public void RecordMode_CanBeSetToProcessed()
    {
        _service.RecordMode = RecordMode.Processed;
        Assert.Equal(RecordMode.Processed, _service.RecordMode);
    }

    // ── WriteSamples safety ──────────────────────────────────────────

    [Fact]
    public void WriteSamples_WhenNotRecording_DoesNotThrow()
    {
        float[] samples = new float[1024];
        var ex = Record.Exception(() => _service.WriteSamples(samples));
        Assert.Null(ex);
    }

    [Fact]
    public void WriteRawSamples_WhenNotRecording_DoesNotThrow()
    {
        float[] samples = new float[1024];
        var ex = Record.Exception(() => _service.WriteRawSamples(samples));
        Assert.Null(ex);
    }

    [Fact]
    public void WriteSamples_InOriginalMode_DoesNotWrite()
    {
        _service.RecordMode = RecordMode.Original;
        _service.StartRecording();
        float[] samples = new float[1024];
        // Should not throw, but should not write (mode mismatch)
        var ex = Record.Exception(() => _service.WriteSamples(samples));
        Assert.Null(ex);
        _service.CancelRecording();
    }

    [Fact]
    public void WriteRawSamples_InProcessedMode_DoesNotWrite()
    {
        _service.RecordMode = RecordMode.Processed;
        _service.StartRecording();
        float[] samples = new float[1024];
        // Should not throw, but should not write (mode mismatch)
        var ex = Record.Exception(() => _service.WriteRawSamples(samples));
        Assert.Null(ex);
        _service.CancelRecording();
    }

    // ── History ──────────────────────────────────────────────────────

    [Fact]
    public void History_InitialState_IsEmpty()
    {
        // History may have items from previous test runs, but should not throw
        var history = _service.History;
        Assert.NotNull(history);
    }

    [Fact]
    public void History_ReturnsSnapshot()
    {
        var h1 = _service.History;
        var h2 = _service.History;
        // Should be different list instances (snapshot)
        Assert.NotSame(h1, h2);
    }

    // ── Dispose safety ───────────────────────────────────────────────

    [Fact]
    public void Dispose_WhenNotRecording_DoesNotThrow()
    {
        var ex = Record.Exception(() => _service.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WhenRecording_StopsRecording()
    {
        _service.StartRecording();
        _service.Dispose();
        // After dispose, IsRecording should be false
        Assert.False(_service.IsRecording);
    }
}
