// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Tests for the recording/export record types: immutability, value equality,
/// with-expressions and JSON round-trips.
/// </summary>
public sealed class RecordingRecordTests
{
    // ── RecordingHistoryItem ─────────────────────────────────────────

    [Fact(DisplayName = "REC-001: RecordingHistoryItem — value equality")]
    public void Rec001_RecordingHistoryItem_ValueEquality()
    {
        var a = new RecordingHistoryItem
        {
            FileName = "test.wav",
            FilePath = "/tmp/test.wav",
            Duration = 10.5,
            FileSize = 1024,
            CreatedAt = new DateTime(2026, 1, 1),
        };

        var b = new RecordingHistoryItem
        {
            FileName = "test.wav",
            FilePath = "/tmp/test.wav",
            Duration = 10.5,
            FileSize = 1024,
            CreatedAt = new DateTime(2026, 1, 1),
        };

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact(DisplayName = "REC-002: RecordingHistoryItem — with expression creates modified copy")]
    public void Rec002_RecordingHistoryItem_WithExpression()
    {
        var original = new RecordingHistoryItem
        {
            FileName = "original.wav",
            FilePath = "/tmp/original.wav",
            Duration = 5.0,
        };

        var modified = original with { FileName = "modified.wav" };

        Assert.Equal("original.wav", original.FileName);
        Assert.Equal("modified.wav", modified.FileName);
        Assert.Equal(original.FilePath, modified.FilePath);
        Assert.NotEqual(original, modified);
    }

    [Fact(DisplayName = "REC-003: RecordingHistoryItem — init-only properties copy equal")]
    public void Rec003_RecordingHistoryItem_InitOnlyProperties()
    {
        var item = new RecordingHistoryItem { FileName = "test.wav" };

        // Record properties with init setter cannot be reassigned after construction.
        // This is a compile-time guarantee — verify the type has value equality.
        var copy = item with { };
        Assert.Equal(item, copy);
    }

    // ── ExportOptions ────────────────────────────────────────────────

    [Fact(DisplayName = "REC-004: ExportOptions — default values")]
    public void Rec004_ExportOptions_DefaultValues()
    {
        var options = new ExportOptions();

        Assert.Equal(ExportFormat.Wav, options.Format);
        Assert.Equal(48000, options.SampleRate);
        Assert.Equal(32, options.BitDepth);
        Assert.Equal(1, options.Channels);
        Assert.Equal(128, options.Bitrate);
        Assert.False(options.UseVBR);
    }

    [Fact(DisplayName = "REC-005: ExportOptions — with expression")]
    public void Rec005_ExportOptions_WithExpression()
    {
        var original = new ExportOptions { SampleRate = 48000, BitDepth = 32 };
        var modified = original with { BitDepth = 16 };

        Assert.Equal(32, original.BitDepth);
        Assert.Equal(16, modified.BitDepth);
        Assert.Equal(48000, modified.SampleRate); // unchanged
    }

    [Fact(DisplayName = "REC-006: ExportOptions — value equality")]
    public void Rec006_ExportOptions_ValueEquality()
    {
        var a = new ExportOptions { Format = ExportFormat.Mp3, Bitrate = 192 };
        var b = new ExportOptions { Format = ExportFormat.Mp3, Bitrate = 192 };

        Assert.Equal(a, b);
    }

    // ── BatchProcessResult ───────────────────────────────────────────

    [Fact(DisplayName = "REC-007: BatchProcessResult — success record")]
    public void Rec007_BatchProcessResult_Success()
    {
        var result = new BatchProcessResult
        {
            InputFile = "input.wav",
            OutputFile = "output.wav",
            Success = true,
        };

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact(DisplayName = "REC-008: BatchProcessResult — failure record")]
    public void Rec008_BatchProcessResult_Failure()
    {
        var result = new BatchProcessResult
        {
            InputFile = "input.wav",
            Success = false,
            Error = "File not found",
        };

        Assert.False(result.Success);
        Assert.Equal("File not found", result.Error);
    }

    // ── JSON serialization round-trip ────────────────────────────────

    [Fact(DisplayName = "REC-014: RecordingHistoryItem — JSON round-trip")]
    public void Rec014_RecordingHistoryItem_JsonRoundTrip()
    {
        var original = new RecordingHistoryItem
        {
            FileName = "test.wav",
            FilePath = "/tmp/test.wav",
            Duration = 10.5,
            FileSize = 2048,
            CreatedAt = new DateTime(2026, 6, 15, 12, 0, 0),
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(original);
        var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<RecordingHistoryItem>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.FileName, deserialized!.FileName);
        Assert.Equal(original.FilePath, deserialized.FilePath);
        Assert.Equal(original.Duration, deserialized.Duration);
        Assert.Equal(original.FileSize, deserialized.FileSize);
    }

    [Fact(DisplayName = "REC-015: ExportOptions — JSON round-trip")]
    public void Rec015_ExportOptions_JsonRoundTrip()
    {
        var original = new ExportOptions
        {
            Format = ExportFormat.Mp3,
            Bitrate = 256,
            UseVBR = true,
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(original);
        var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<ExportOptions>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(ExportFormat.Mp3, deserialized!.Format);
        Assert.Equal(256, deserialized.Bitrate);
        Assert.True(deserialized.UseVBR);
    }
}
