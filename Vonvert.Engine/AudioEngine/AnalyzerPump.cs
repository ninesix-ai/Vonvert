// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/*
 * AnalyzerPump — decouples heavy frequency/loudness analysis from the
 * real-time audio thread.
 *
 * The audio (DSP) thread only copies each processed block into a
 * zero-allocation SampleQueue via Submit(); a dedicated low-priority
 * worker thread drains the queue and runs the analyzers on it. This keeps
 * FFT / pitch-detection / loudness statistics off the time-critical audio
 * path so a slow analysis pass can never starve the render buffer.
 *
 * The sink callback is invoked on the worker thread, never on the caller's
 * thread. Consumers stay thread-safe because each analyzer publishes its
 * result through a volatile snapshot that the UI reads independently.
 */
public sealed class AnalyzerPump : IDisposable
{
    // ~1 s of buffering; if the worker falls behind, the queue drops the
    // overflow rather than growing without bound — visualization may skip a
    // frame but audio is never affected.
    private const int QueueCapacity = 48000;
    private const int ChunkSize     = 4096;

    private readonly SampleQueue _queue = new(QueueCapacity);
    private readonly float[] _scratch = new float[ChunkSize];
    private readonly Action<ReadOnlySpan<float>> _sink;

    private Thread? _worker;
    private volatile bool _running;

    public AnalyzerPump(Action<ReadOnlySpan<float>> sink)
        => _sink = sink ?? throw new ArgumentNullException(nameof(sink));

    /// <summary>Number of samples currently awaiting analysis (diagnostics/tests).</summary>
    public int Pending => _queue.Count;

    /// <summary>
    /// Called from the audio thread: copy the block and return immediately.
    /// Performs no analysis and no heap allocation.
    /// </summary>
    public void Submit(ReadOnlySpan<float> block)
    {
        if (!_running) return;
        _queue.Enqueue(block);
    }

    /// <summary>Start the analysis worker. Idempotent.</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        _worker = new Thread(Run)
        {
            Name         = "Vonvert.Analysis",
            Priority     = ThreadPriority.BelowNormal,
            IsBackground = true
        };
        _worker.Start();
    }

    /// <summary>Stop the analysis worker and drop any queued samples. Idempotent.</summary>
    public void Stop()
    {
        _running = false;
        var w = _worker;
        _worker = null;
        if (w != null && w.IsAlive)
        {
            if (!w.Join(1000))
                AppLog.Warning("AnalyzerPump worker did not exit within 1s");
        }
        _queue.Purge();
    }

    private void Run()
    {
        while (_running)
        {
            int read = _queue.Dequeue(_scratch);
            if (read > 0)
            {
                try
                {
                    _sink(_scratch.AsSpan(0, read));
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex, "AnalyzerPump sink failed");
                }
            }
            else
            {
                Thread.Sleep(2);
            }
        }
    }

    public void Dispose() => Stop();
}
