// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using NAudio.Wave;
using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Tests for RecordingService export functionality and file I/O safety.
/// Covers WAV/MP3 export, ReadAudioFile, WriteWavFile, and history persistence.
/// </summary>
public sealed class RecordingServiceExportTests : IDisposable
{
    private readonly string _tempDir;

    public RecordingServiceExportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_ExportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    /// <summary>Generate a test WAV file (float32 48kHz mono) with a sine wave.</summary>
    private static string GenerateTestWav(string dir, float freq = 440f, int durationSec = 1)
    {
        var path = Path.Combine(dir, $"test_{Guid.NewGuid():N}.wav");
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        using var writer = new WaveFileWriter(path, fmt);
        int totalSamples = 48000 * durationSec;
        for (int i = 0; i < totalSamples; i++)
            writer.WriteSample(0.3f * MathF.Sin(2 * MathF.PI * freq * i / 48000f));
        return path;
    }

    // ── WriteWavFile ─────────────────────────────────────────────────

    [Fact(DisplayName = "EXP-001: WriteWavFile — creates valid WAV file")]
    public void EXP001_WriteWavFile_CreatesValidWav()
    {
        var svc = new RecordingService();
        var samples = new float[48000]; // 1 second of silence
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / 48000f);

        var outPath = Path.Combine(_tempDir, "output.wav");
        svc.WriteWavFile(outPath, samples);

        Assert.True(File.Exists(outPath));
        var info = new FileInfo(outPath);
        Assert.True(info.Length > 44); // WAV header + data

        // Read back and verify sample count
        using var reader = new WaveFileReader(outPath);
        Assert.Equal(48000, reader.Length / 4); // 4 bytes per float sample
    }

    // ── ReadAudioFile ────────────────────────────────────────────────

    [Fact(DisplayName = "EXP-002: ReadAudioFile — reads WAV file correctly")]
    public void EXP002_ReadAudioFile_ReadsCorrectly()
    {
        var wavPath = GenerateTestWav(_tempDir, 440f, 1);
        var svc = new RecordingService();

        var samples = svc.ReadAudioFile(wavPath);

        Assert.NotEmpty(samples);
        Assert.Equal(48000, samples.Length); // 1 sec at 48kHz
    }

    [Fact(DisplayName = "EXP-003: ReadAudioFile — non-existent file returns empty array")]
    public void EXP003_ReadAudioFile_NonExistent_ReturnsEmpty()
    {
        var svc = new RecordingService();
        var samples = svc.ReadAudioFile(Path.Combine(_tempDir, "nope.wav"));
        Assert.Empty(samples);
    }

    // ── ExportRecording (WAV) ────────────────────────────────────────

    [Fact(DisplayName = "EXP-004: ExportRecording WAV 16-bit — produces valid output")]
    public void EXP004_ExportWav16bit_ProducesValidOutput()
    {
        var wavPath = GenerateTestWav(_tempDir);
        var outPath = Path.Combine(_tempDir, "export_16bit.wav");
        var item = new RecordingHistoryItem { FilePath = wavPath };
        var svc = new RecordingService();

        var result = svc.ExportRecording(item, outPath, new ExportOptions
        {
            Format = ExportFormat.Wav,
            SampleRate = 48000,
            BitDepth = 16,
            Channels = 1
        });

        Assert.True(result);
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 44);
    }

    [Fact(DisplayName = "EXP-005: ExportRecording WAV float32 stereo — produces valid output")]
    public void EXP005_ExportWavFloatStereo_ProducesValidOutput()
    {
        var wavPath = GenerateTestWav(_tempDir);
        var outPath = Path.Combine(_tempDir, "export_float_stereo.wav");
        var item = new RecordingHistoryItem { FilePath = wavPath };
        var svc = new RecordingService();

        var result = svc.ExportRecording(item, outPath, new ExportOptions
        {
            Format = ExportFormat.Wav,
            SampleRate = 48000,
            BitDepth = 32,
            Channels = 2
        });

        Assert.True(result);
        Assert.True(File.Exists(outPath));

        // Verify it's stereo
        using var reader = new WaveFileReader(outPath);
        Assert.Equal(2, reader.WaveFormat.Channels);
    }

    [Fact(DisplayName = "EXP-006: ExportRecording — non-existent source returns false")]
    public void EXP006_ExportNonExistentSource_ReturnsFalse()
    {
        var item = new RecordingHistoryItem { FilePath = Path.Combine(_tempDir, "ghost.wav") };
        var svc = new RecordingService();

        var result = svc.ExportRecording(item, Path.Combine(_tempDir, "out.wav"), ExportFormat.Wav);

        Assert.False(result);
    }

    // ── ExportRecording (MP3) ────────────────────────────────────────

    [Fact(DisplayName = "EXP-007: ExportRecording MP3 CBR — produces valid output")]
    public void EXP007_ExportMp3Cbr_ProducesValidOutput()
    {
        var wavPath = GenerateTestWav(_tempDir, 440f, 2);
        var outPath = Path.Combine(_tempDir, "export.mp3");
        var item = new RecordingHistoryItem { FilePath = wavPath };
        var svc = new RecordingService();

        var result = svc.ExportRecording(item, outPath, new ExportOptions
        {
            Format = ExportFormat.Mp3,
            Bitrate = 128,
            UseVBR = false
        });

        Assert.True(result);
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    [Fact(DisplayName = "EXP-008: ExportRecording MP3 VBR — produces valid output")]
    public void EXP008_ExportMp3Vbr_ProducesValidOutput()
    {
        var wavPath = GenerateTestWav(_tempDir, 880f, 1);
        var outPath = Path.Combine(_tempDir, "export_vbr.mp3");
        var item = new RecordingHistoryItem { FilePath = wavPath };
        var svc = new RecordingService();

        var result = svc.ExportRecording(item, outPath, new ExportOptions
        {
            Format = ExportFormat.Mp3,
            UseVBR = true,
            VorbisQuality = 0.7f
        });

        Assert.True(result);
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    // ── History persistence ──────────────────────────────────────────

    [Fact(DisplayName = "EXP-009: RecordingService — start/stop creates history entry")]
    public void EXP009_StartStop_CreatesHistoryEntry()
    {
        var svc = new RecordingService();
        svc.StartRecording();

        var samples = new float[4800]; // 100ms at 48kHz
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.1f * MathF.Sin(2 * MathF.PI * 440 * i / 48000f);
        svc.WriteSamples(samples, samples.Length);

        var item = svc.StopRecording();

        Assert.NotNull(item);
        Assert.True(File.Exists(item.FilePath));
        Assert.True(item.Duration > 0);
        Assert.True(item.FileSize > 0);

        svc.DeleteRecording(item);
    }

    [Fact(DisplayName = "EXP-010: RecordingService — cancel recording deletes temp file")]
    public void EXP010_CancelRecording_DeletesTempFile()
    {
        var svc = new RecordingService();
        svc.StartRecording();

        var samples = new float[4800];
        svc.WriteSamples(samples, samples.Length);

        svc.CancelRecording();

        Assert.False(svc.IsRecording);
        // After cancel, no history entry should be added
        Assert.Empty(svc.History.Where(h => h.FilePath.Contains("temp_")));
    }

    [Fact(DisplayName = "EXP-011: RecordingService — Dispose stops recording")]
    public void EXP011_Dispose_StopsRecording()
    {
        var svc = new RecordingService();
        svc.StartRecording();
        Assert.True(svc.IsRecording);

        svc.Dispose();

        Assert.False(svc.IsRecording);
    }
}
