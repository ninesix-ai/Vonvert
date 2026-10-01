// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Real-time time-domain waveform capture for audio visualization.
/// Thread-safe: DSP thread calls FeedSamples(), UI thread reads GetWaveform().
/// Uses lock-free double-buffering for waveform output.
/// </summary>
public sealed class WaveformCapture : IWaveformCapture
{
    private readonly int _bufferSize;

    // Circular sample buffer (DSP thread writes)
    private readonly float[] _sampleBuf;
    private int _writePos;

    // Double-buffered waveform output (lock-free)
    private float[] _waveA;
    private float[] _waveB;
    private volatile float[] _activeWave;  // what the UI reads
    private volatile float[] _backWave;    // what the DSP writes into

    // Peak hold for level indicator
    private volatile float _activePeak;
    private volatile float _backPeak;

    public WaveformCapture(int bufferSize = 1024)
    {
        _bufferSize = bufferSize;
        _sampleBuf = new float[bufferSize];
        _waveA = new float[bufferSize];
        _waveB = new float[bufferSize];
        _activeWave = _waveA;
        _backWave = _waveB;
    }

    /// <summary>
    /// Feed raw audio samples from the DSP thread.
    /// Stores a snapshot of the waveform for UI consumption.
    /// Non-allocating on the hot path.
    /// </summary>
    public void FeedSamples(ReadOnlySpan<float> samples)
    {
        float peak = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            _sampleBuf[_writePos] = samples[i];
            _writePos = (_writePos + 1) % _bufferSize;
            peak = MathF.Max(peak, MathF.Abs(samples[i]));
        }

        // Copy circular buffer to linear back-buffer (newest data at the right)
        var target = _backWave;
        int start = _writePos; // oldest sample
        for (int i = 0; i < _bufferSize; i++)
        {
            int idx = (start + i) % _bufferSize;
            target[i] = _sampleBuf[idx];
        }

        _backPeak = peak;

        // Swap buffers (atomic reference swap, no lock needed)
        var tempWave = _activeWave;
        _activeWave = _backWave;
        _backWave = tempWave;

        var tempPeak = _activePeak;
        _activePeak = _backPeak;
        _backPeak = tempPeak;
    }

    /// <summary>
    /// Get the current waveform data (-1..1 normalized). Safe to call from UI thread.
    /// Returns a snapshot that won't be modified. Length = BufferSize.
    /// </summary>
    public float[] GetWaveform() => _activeWave;

    /// <summary>Get the buffer size (number of samples in the waveform).</summary>
    public int BufferSize => _bufferSize;

    /// <summary>Get the current peak level (0..1). Safe to call from UI thread.</summary>
    public float GetPeak() => _activePeak;

    /// <summary>Reset the capture state (e.g., when engine stops).</summary>
    public void Reset()
    {
        _writePos = 0;
        Array.Clear(_sampleBuf);
        Array.Clear(_waveA);
        Array.Clear(_waveB);
        _activePeak = 0f;
        _backPeak = 0f;
        AppLog.Debug("[WaveformCapture] Reset");
    }
}
