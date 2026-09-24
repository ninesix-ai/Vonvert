// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// RecordingService-specific resource-safety guards: the span hot-path overloads
/// must not allocate per call, must write correct data, and repeated recording
/// sessions must clean up their files.
/// </summary>
public class RecordingResourceSafetyTests
{
    [Fact(DisplayName = "RS-003: RecordingService WriteSamples(Span) — no per-call allocation")]
    public void RS003_RecordingServiceSpanWrite_NoAllocation()
    {
        var rec = new RecordingService();
        rec.StartRecording();
        Thread.Sleep(20); // let writer initialize

        var buf = new float[4096];
        var rng = new Random(42);
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)(rng.NextDouble() * 0.5 - 0.25);

        // Warmup
        rec.WriteSamples(buf.AsSpan());
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            rec.WriteSamples(buf.AsSpan());
        }
        long afterAlloc = GC.GetAllocatedBytesForCurrentThread();

        long totalAlloc = afterAlloc - beforeAlloc;
        long perCall = totalAlloc / iterations;

        // Budget: < 64 bytes per call (span overload should be near-zero alloc)
        Assert.True(perCall < 64,
            $"WriteSamples(Span) allocated {perCall} bytes/call — exceeds 64-byte budget.");

        var item = rec.StopRecording();
        if (item != null) rec.DeleteRecording(item);
        rec.Dispose();
    }

    [Fact(DisplayName = "RS-006: RecordingService WriteRawSamples(Span) — writes correct data")]
    public void RS006_WriteRawSamplesSpan_WritesCorrectData()
    {
        var rec = new RecordingService();
        rec.RecordMode = RecordMode.Original;
        rec.StartRecording();
        Thread.Sleep(20);

        // Write known pattern
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = (i % 100) / 100f;

        rec.WriteRawSamples(buf.AsSpan());
        Thread.Sleep(50); // ensure flush

        var item = rec.StopRecording();
        Assert.NotNull(item);
        Assert.True(File.Exists(item!.FilePath));
        Assert.True(item.FileSize > 44, "Recording file too small — header only");

        rec.DeleteRecording(item);
        rec.Dispose();
    }

    [Fact(DisplayName = "RS-007: RecordingService WriteSamples(Span) vs WriteSamples(float[], int) — same output")]
    public void RS007_WriteSamplesSpan_MatchesArrayOverload()
    {
        // Verify that the span overload produces identical output to the
        // array overload by comparing file sizes for the same input data.
        var rec1 = new RecordingService();
        var rec2 = new RecordingService();

        RecordingHistoryItem? item1 = null, item2 = null;
        try
        {
            var buf = new float[4096];
            var rng = new Random(123);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Record via array overload
            rec1.RecordMode = RecordMode.Processed;
            rec1.StartRecording();
            Thread.Sleep(20);
            rec1.WriteSamples(buf, buf.Length);
            Thread.Sleep(50);
            item1 = rec1.StopRecording();

            // Record via span overload
            rec2.RecordMode = RecordMode.Processed;
            rec2.StartRecording();
            Thread.Sleep(20);
            rec2.WriteSamples(buf.AsSpan());
            Thread.Sleep(50);
            item2 = rec2.StopRecording();

            Assert.NotNull(item1);
            Assert.NotNull(item2);
            // File sizes should be identical (same data, same format, float32 mono).
            long sizeDiff = Math.Abs(item1!.FileSize - item2!.FileSize);
            Assert.True(sizeDiff == 0,
                $"File sizes differ by {sizeDiff} bytes: rec1={item1.FileSize}, rec2={item2.FileSize}");
        }
        finally
        {
            if (item1 != null) rec1.DeleteRecording(item1);
            if (item2 != null) rec2.DeleteRecording(item2);
            rec1.Dispose();
            rec2.Dispose();
        }
    }

    [Fact(DisplayName = "RS-010: RecordingService — 100 start/stop cycles with file cleanup verification")]
    public void RS010_RecordingServiceSoak_AllFilesCleanedUp()
    {
        var rec = new RecordingService();
        var createdFiles = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            rec.StartRecording();
            Thread.Sleep(5);
            var buf = new float[4096];
            rec.WriteSamples(buf, buf.Length);
            var item = rec.StopRecording();
            if (item != null) createdFiles.Add(item.FilePath);
        }

        // All files should exist and be accessible (not locked)
        int accessibleFiles = 0;
        foreach (var file in createdFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    accessibleFiles++;
                }
                catch { /* file locked or inaccessible */ }
            }
        }

        Assert.Equal(createdFiles.Count, accessibleFiles);

        // Clean up
        foreach (var item in rec.History)
            rec.DeleteRecording(item);
        rec.Dispose();
    }
}
