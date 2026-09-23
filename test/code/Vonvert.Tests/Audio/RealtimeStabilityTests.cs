// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Threading;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Regression tests for real-time audio-thread stability:
///   - the DSP chain hot path allocates nothing and honors live enable/disable
///   - heavy analysis (FFT / pitch / loudness) runs off the audio thread
///   - the spectrum analyzer publishes without per-frame allocation
/// </summary>
public class RealtimeStabilityTests
{
    // A minimal effect that stamps buffer[0] so we can observe whether the
    // chain invoked Process() (enabled) or skipped it (disabled).
    private sealed class MarkerEffect : IAudioEffect
    {
        public string Name { get; init; } = "Marker";
        public bool IsEnabled { get; set; } = true;
        public float Marker { get; init; } = 7f;
        public void Process(Span<float> buffer) { if (buffer.Length > 0) buffer[0] = Marker; }
        public void Reset() { }
    }

    // ── DSP chain hot path ────────────────────────────────────────────────

    [Fact(DisplayName = "RT-001: DSPChain.Process performs no heap allocation per block")]
    public void DSPChainProcess_ZeroAllocation()
    {
        var chain = new DSPChain();
        for (int i = 0; i < 6; i++)
            chain.Add(new MarkerEffect { Name = $"E{i}", IsEnabled = i % 2 == 0 });

        var buf = new float[4096];
        chain.Process(buf); // warmup / JIT

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        const int iterations = 200;
        for (int i = 0; i < iterations; i++)
            chain.Process(buf);
        long perBlock = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;

        Assert.True(perBlock < 64,
            $"DSPChain.Process allocated {perBlock} bytes/block — expected ~0 (no lock/ToArray).");
    }

    [Fact(DisplayName = "RT-002: DSPChain honors a runtime IsEnabled toggle without a structural change")]
    public void DSPChain_LiveEnabledToggle()
    {
        var chain = new DSPChain();
        var fx = new MarkerEffect { Marker = 7f };
        chain.Add(fx);

        var buf = new float[16];
        chain.Process(buf);
        Assert.Equal(7f, buf[0]);

        buf[0] = 0f;
        fx.IsEnabled = false; // no Add/Remove
        chain.Process(buf);
        Assert.Equal(0f, buf[0]);

        buf[0] = 0f;
        fx.IsEnabled = true;
        chain.Process(buf);
        Assert.Equal(7f, buf[0]);
    }

    // ── Analyzer thread decoupling ────────────────────────────────────────

    [Fact(DisplayName = "RT-003: AnalyzerPump runs its sink on a worker thread, not the caller")]
    public void AnalyzerPump_RunsOnWorkerThread()
    {
        int callerThread = Environment.CurrentManagedThreadId;
        int sinkThread = 0;
        using var ran = new ManualResetEventSlim(false);

        using var pump = new AnalyzerPump(_ =>
        {
            sinkThread = Environment.CurrentManagedThreadId;
            ran.Set();
        });

        pump.Start();
        pump.Submit(new float[128]);

        Assert.True(ran.Wait(2000), "sink callback was never invoked");
        Assert.NotEqual(0, sinkThread);
        Assert.NotEqual(callerThread, sinkThread);
    }

    [Fact(DisplayName = "RT-004: AnalyzerPump stops invoking the sink after Stop()")]
    public void AnalyzerPump_NoCallbacksAfterStop()
    {
        int calls = 0;
        using var pump = new AnalyzerPump(_ => Interlocked.Increment(ref calls));

        pump.Start();
        pump.Submit(new float[64]);
        SpinWait.SpinUntil(() => Volatile.Read(ref calls) > 0, 2000);
        Assert.True(Volatile.Read(ref calls) > 0);

        pump.Stop();
        int afterStop = Volatile.Read(ref calls);

        for (int i = 0; i < 50; i++) pump.Submit(new float[64]);
        Thread.Sleep(50);

        Assert.Equal(afterStop, Volatile.Read(ref calls));
    }

    [Fact(DisplayName = "RT-005: NullAudioProcessor analyzes via the pump (spectrum bars populate)")]
    public void Processor_AnalyzesThroughPump()
    {
        var proc = new NullAudioProcessor();
        proc.OnStart();
        try
        {
            var buf = new float[4096];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / 48000f);

            bool lit = false;
            SpinWait.SpinUntil(() =>
            {
                for (int n = 0; n < 8; n++) proc.PostAnalyze(buf, buf.Length);
                foreach (var b in proc.Spectrum.GetSpectrumBars())
                    if (b > 0.01f) { lit = true; break; }
                return lit;
            }, 3000);

            Assert.True(lit, "Spectrum never received analysis through the pump");
        }
        finally
        {
            proc.OnStop();
            proc.Dispose();
        }
    }

    // ── Spectrum zero-allocation ──────────────────────────────────────────

    [Fact(DisplayName = "RT-006: SpectrumAnalyzer publishes across frames without allocating")]
    public void SpectrumAnalyzer_ZeroAllocation()
    {
        var sa = new SpectrumAnalyzer();
        var buf = new float[8192];
        for (int i = 0; i < buf.Length; i++)
            buf[i] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / 48000f);

        sa.FeedSamples(buf); // warmup

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 50; i++) sa.FeedSamples(buf);
        long total = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(total < 512,
            $"SpectrumAnalyzer allocated {total} bytes across 50 feeds — expected ~0 (double-buffered).");
    }
}
