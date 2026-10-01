// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Threading;
using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  PW-001 ~ PW-004: dry/wet waveform taps + spectrogram feed wiring.
//  Mirrors the engine worker order: PreMix → Process → PostAnalyze.
//  A tap wired onto the real-time path by mistake would break PW-004;
//  a tap never fed at all breaks PW-001/002/003.
// ═══════════════════════════════════════════════════════════════════

public sealed class ProcessorAnalyzerWiringTests
{
    private const int Block = 480;   // 10 ms @ 48 kHz

    private static float[] Sine(float hz, int n)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = 0.7f * MathF.Sin(2f * MathF.PI * hz * i / 48000f);
        return s;
    }

    private static void RunBlocks(NullAudioProcessor p, int blocks)
    {
        var work = Sine(1000f, Block);
        for (int b = 0; b < blocks; b++)
        {
            var span = work.AsSpan();
            p.PreMix(span, Block);
            p.Process(span);
            p.PostAnalyze(span, Block);
        }
    }

    [Fact(DisplayName = "PW-001: PreMix feeds the dry input waveform tap")]
    public void PW001_InputWaveformFedInPreMix()
    {
        var p = new NullAudioProcessor();
        RunBlocks(p, 4);
        Assert.True(p.InputWaveform.GetPeak() > 0.1f, "dry tap has no signal");
        p.Dispose();
    }

    [Fact(DisplayName = "PW-002: PostAnalyze feeds the wet output waveform tap")]
    public void PW002_OutputWaveformFedInPostAnalyze()
    {
        var p = new NullAudioProcessor();
        RunBlocks(p, 4);
        Assert.True(p.OutputWaveform.GetPeak() > 0f, "wet tap has no signal");
        p.Dispose();
    }

    [Fact(DisplayName = "PW-003: spectrogram fills on the analyzer pump thread, not the audio path")]
    public void PW003_SpectrogramFedViaPump()
    {
        var p = new NullAudioProcessor();
        p.OnStart();
        try
        {
            RunBlocks(p, 60);   // 600 ms of audio → pump should produce frames
            // The pump runs on a worker thread; poll briefly for the first frame.
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (p.Spectrogram.TimeFrames == 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(20);
            Assert.True(p.Spectrogram.TimeFrames > 0, "pump never fed the spectrogram");
        }
        finally { p.OnStop(); p.Dispose(); }
    }

    [Fact(DisplayName = "PW-004: the audio-thread tap path (PreMix/PostAnalyze) stays allocation-free")]
    public void PW004_AudioPathZeroAlloc()
    {
        var p = new NullAudioProcessor();
        // Run with the pump live so Submit's enqueue path is inside the probe
        // radius — the same code the real-time worker executes in production.
        p.OnStart();
        try
        {
            RunBlocks(p, 50);                               // warmup
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

            var work = Sine(1000f, Block);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int b = 0; b < 500; b++)
            {
                var span = work.AsSpan();
                p.PreMix(span, Block);
                p.Process(span);
                p.PostAnalyze(span, Block);
            }
            long perBlock = (GC.GetAllocatedBytesForCurrentThread() - before) / 500;
            Assert.True(perBlock < 64, $"audio path allocated {perBlock} B/block — taps must not allocate");
        }
        finally { p.OnStop(); p.Dispose(); }
    }
}
