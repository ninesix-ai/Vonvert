// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  WaveformCapture — WC-001 ~ WC-005 (lock-free double buffer)
// ═══════════════════════════════════════════════════════════════════

public sealed class WaveformCaptureTests
{
    [Fact(DisplayName = "WC-001: FeedSamples linearizes ring, newest at the right")]
    public void WC001_RingLinearization_NewestAtRight()
    {
        var cap = new WaveformCapture(bufferSize: 8);
        cap.FeedSamples(new float[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var wave = cap.GetWaveform();
        Assert.Equal(8, wave.Length);
        // Second feed shifts the window; newest samples must sit at the tail.
        var next = new float[] { 9, 10, 11, 12, 13, 14, 15, 16 };
        cap.FeedSamples(next);
        wave = cap.GetWaveform();
        Assert.Equal(16f, wave[7]);
        Assert.Equal(9f, wave[0]);
    }

    [Fact(DisplayName = "WC-002: GetPeak reports max |sample| of the last feed")]
    public void WC002_Peak()
    {
        var cap = new WaveformCapture(bufferSize: 4);
        cap.FeedSamples(new float[] { 0.1f, -0.9f, 0.2f, 0f });
        Assert.Equal(0.9f, cap.GetPeak(), 5);
    }

    [Fact(DisplayName = "WC-003: Reset clears buffers and peak")]
    public void WC003_Reset()
    {
        var cap = new WaveformCapture(bufferSize: 4);
        cap.FeedSamples(new float[] { 1, 2, 3, 4 });
        cap.Reset();
        Assert.All(cap.GetWaveform(), v => Assert.Equal(0f, v));
        Assert.Equal(0f, cap.GetPeak());
    }

    [Fact(DisplayName = "WC-004: FeedSamples is allocation-free on the hot path")]
    public void WC004_ZeroAllocation()
    {
        var cap = new WaveformCapture(bufferSize: 1024);
        var buf = new float[480];
        for (int i = 0; i < buf.Length; i++) buf[i] = (i % 97) / 97f - 0.5f;

        for (int i = 0; i < 50; i++) cap.FeedSamples(buf);   // warmup
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++) cap.FeedSamples(buf);
        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 500;

        Assert.True(perCall < 64, $"allocated {perCall} B/call — hot path must be near-zero");
    }

    [Fact(DisplayName = "WC-005: concurrent feed + read never throws or tears length")]
    public void WC005_ConcurrentFeedRead()
    {
        var cap = new WaveformCapture(bufferSize: 512);
        var feed = new System.Threading.Thread(() =>
        {
            var block = new float[128];
            for (int round = 0; round < 2000; round++)
            {
                for (int i = 0; i < block.Length; i++) block[i] = MathF.Sin(round + i);
                cap.FeedSamples(block);
            }
        });
        feed.Start();
        for (int i = 0; i < 2000; i++)
        {
            var wave = cap.GetWaveform();
            Assert.Equal(512, wave.Length);          // snapshot length is stable
            _ = cap.GetPeak();
        }
        feed.Join();
    }
}
