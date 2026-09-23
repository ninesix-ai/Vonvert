// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

// ── Engine lifecycle ──────────────────────────────────────────────────────

/// <summary>Operational status of the voice engine.</summary>
public enum EngineStatus
{
    Idle,
    Active,
    Faulted
}

// ── A/B compare ──────────────────────────────────────────────────────────

/// <summary>
/// Determines what the listener hears at the output.
/// Used for A/B comparison between dry, wet, and normal signals.
/// </summary>
public enum CompareMode
{
    /// <summary>Standard DSP processing — full effect chain applied.</summary>
    Normal,

    /// <summary>Bypass all DSP — the raw microphone signal passes through untouched.</summary>
    Dry,

    /// <summary>Effect-only — subtract the dry signal so only the "coloration" is heard.</summary>
    WetOnly
}

// ── Quality tier ─────────────────────────────────────────────────────────

/// <summary>
/// Two user-facing quality presets that trade latency against
/// timbre fidelity of the pitch-shifting algorithm.
/// </summary>
public enum AudioQualityMode
{
    /// <summary>Minimum end-to-end latency — ideal for gaming and live chat.</summary>
    LowLatency,

    /// <summary>Best audio quality — ideal for streaming and recording. Default.</summary>
    HighQuality
}

// ── Device descriptor ────────────────────────────────────────────────────

/// <summary>Immutable snapshot of a system audio device.</summary>
public record AudioDevice(
    string Id,
    string Name,
    int Channels,
    int SampleRate,
    int BitsPerSample)
{
    public override string ToString()
        => $"{Name}  ({SampleRate}Hz / {Channels}ch)";
}

// ── Engine configuration ─────────────────────────────────────────────────

/// <summary>
/// Mutable bag of settings that control how the voice engine captures,
/// processes, and routes audio.  Persisted to disk as JSON by the UI layer.
/// </summary>
public class EngineSettings
{
    // Device selection
    public string InputDeviceId   { get; set; } = string.Empty;
    public string OutputDeviceId  { get; set; } = string.Empty;

    // Buffer / rate
    public int LatencyMs          { get; set; } = 10;
    public int SampleRate         { get; set; } = AudioConstants.EngineRate;

    // Gain & comparison
    public float PreAmpGain       { get; set; } = 1.0f;
    public CompareMode Compare    { get; set; } = CompareMode.Normal;

    // Monitoring
    public bool HearMyself        { get; set; } = false;

    // Quality tier
    /// <summary>
    /// Selects between LowLatency (5 ms capture) and HighQuality
    /// (standard buffers) audio behaviour.
    /// </summary>
    public AudioQualityMode QualityMode { get; set; } = AudioQualityMode.HighQuality;

    /// <summary>
    /// Backward-compatible toggle — equivalent to setting QualityMode.
    /// true ⇔ LowLatency; kept so existing callers compile unchanged.
    /// </summary>
    public bool LowLatency
    {
        get => QualityMode == AudioQualityMode.LowLatency;
        set => QualityMode = value ? AudioQualityMode.LowLatency : AudioQualityMode.HighQuality;
    }

    // Auto gain
    public bool AutoGain          { get; set; } = false;
}

// ── Runtime statistics ───────────────────────────────────────────────────

/// <summary>
/// Real-time engine metrics. Fields are written by the DSP thread and
/// read by the UI thread (80 ms timer). All property accessors use
/// <see cref="Volatile"/> to guarantee visibility across threads without
/// a lock.  On x64, aligned 32/64-bit reads are atomic; Volatile ensures
/// the compiler and CPU do not cache stale values.
/// </summary>
public class EngineStats
{
    private double _latencyMs;
    private float  _processorLoad;
    private float  _inputLevel;
    private float  _outputLevel;
    private int    _underruns;
    private int    _dspExceptions;

    /// <summary>End-to-end latency in milliseconds (capture → output).</summary>
    public double LatencyMs
    {
        get => Volatile.Read(ref _latencyMs);
        set => Volatile.Write(ref _latencyMs, value);
    }

    /// <summary>Fraction of CPU time consumed by the DSP thread (0–1).</summary>
    public float  ProcessorLoad
    {
        get => Volatile.Read(ref _processorLoad);
        set => Volatile.Write(ref _processorLoad, value);
    }

    /// <summary>Peak input sample amplitude (0 = silence, 1 = full scale).</summary>
    public float  InputLevel
    {
        get => Volatile.Read(ref _inputLevel);
        set => Volatile.Write(ref _inputLevel, value);
    }

    /// <summary>Peak output sample amplitude after DSP processing.</summary>
    public float  OutputLevel
    {
        get => Volatile.Read(ref _outputLevel);
        set => Volatile.Write(ref _outputLevel, value);
    }

    /// <summary>Cumulative count of ring-buffer write overflows.</summary>
    public int    Underruns
    {
        get => Volatile.Read(ref _underruns);
        set => Volatile.Write(ref _underruns, value);
    }

    /// <summary>Number of DSP exceptions caught since engine start.</summary>
    public int    DspExceptions
    {
        get => Volatile.Read(ref _dspExceptions);
        set => Volatile.Write(ref _dspExceptions, value);
    }

    /// <summary>Human-readable status label (e.g. "Active", "Idle").</summary>
    /// <remarks>Reference assignment is atomic on x64; not accessed from the DSP thread.</remarks>
    public string Status         { get; set; } = "Idle";
}
