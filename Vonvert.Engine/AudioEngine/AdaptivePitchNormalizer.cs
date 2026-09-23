// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Threading;

namespace Vonvert.Engine.AudioEngine;

/// <summary>Read-side consumed by the pitch shifter on the audio thread.</summary>
public interface IAutoPitchSource
{
    bool  Enabled   { get; }
    bool  HasTarget { get; }
    float Semitones { get; }
}

/// <summary>Write-side configuration from the UI / coordinator thread.</summary>
public interface IPitchNormalizerControl
{
    bool  Enabled           { get; set; }
    float TargetF0Hz        { get; set; }
    float FallbackSemitones { get; set; }
    void  Reset();
}

/// <summary>
/// Closed-loop pitch-register normalizer. Consumes voiced F0 observations on the
/// analyzer worker thread, estimates a stable median fundamental over a short
/// window, and publishes the semitone offset that steers the speaker toward a
/// target F0. The offset is dead-banded and rate-limited so output never warbles.
/// </summary>
public sealed class AdaptivePitchNormalizer : IAutoPitchSource, IPitchNormalizerControl
{
    private const int   WindowFrames   = 36;      // ~1.5 s of voiced snapshots @ ~42.7 ms
    private const int   ReadyMinFrames = 10;      // frames before the loop takes over
    private const float DeadBandSt     = 0.75f;   // ignore sub-threshold moves
    private const float MaxStepSt      = 0.25f;   // rate limit per Update
    private const float ClampSt        = 12f;     // musical sanity
    private const float MinF0          = 60f;
    private const float MaxF0          = 1000f;
    private const float MinTargetF0    = 80f;
    private const float MaxTargetF0    = 400f;

    private readonly object _gate = new();
    private readonly float[] _window  = new float[WindowFrames];
    private readonly float[] _sortBuf = new float[WindowFrames];
    private int _head, _count;
    private float _current;                        // protected by _gate

    private int   _enabled;                         // 0/1
    private int   _hasTarget;                       // 0/1
    private float _targetF0Hz = 220f;
    private float _fallback;
    private int   _semBits;                          // atomic publish (float bits)

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set { Volatile.Write(ref _enabled, value ? 1 : 0); if (!value) Reset(); }
    }

    public float TargetF0Hz
    {
        get => Volatile.Read(ref _targetF0Hz);
        set => Volatile.Write(ref _targetF0Hz, value);
    }

    public float FallbackSemitones
    {
        get => Volatile.Read(ref _fallback);
        set => Volatile.Write(ref _fallback, value);
    }

    public bool  HasTarget => Volatile.Read(ref _hasTarget) != 0;
    public float Semitones => BitConverter.Int32BitsToSingle(Volatile.Read(ref _semBits));

    public AdaptivePitchNormalizer() => Publish(0f);

    public void Reset()
    {
        lock (_gate) { Array.Clear(_window); _head = 0; _count = 0; _current = 0f; }
        Volatile.Write(ref _hasTarget, 0);
        Publish(FallbackSemitones);
    }

    /// <summary>Called on the analyzer worker thread for each F0 snapshot.</summary>
    public void Update(bool isVoiced, float f0Hz)
    {
        if (!Enabled) return;

        float target  = TargetF0Hz;
        float publish = FallbackSemitones;

        lock (_gate)
        {
            bool usable = isVoiced && f0Hz >= MinF0 && f0Hz <= MaxF0
                          && target >= MinTargetF0 && target <= MaxTargetF0;
            if (usable)
            {
                _window[_head] = f0Hz;
                _head = (_head + 1) % WindowFrames;
                if (_count < WindowFrames) _count++;

                if (_count >= ReadyMinFrames)
                {
                    float median  = Median(_count);
                    float desired = Math.Clamp(12f * MathF.Log2(target / median), -ClampSt, ClampSt);
                    if (MathF.Abs(desired - _current) >= DeadBandSt)
                        _current += Math.Clamp(desired - _current, -MaxStepSt, MaxStepSt);
                    Volatile.Write(ref _hasTarget, 1);
                }
            }
            // not usable -> hold: _current / _window untouched

            publish = Volatile.Read(ref _hasTarget) != 0 ? _current : FallbackSemitones;
        }
        Publish(publish);
    }

    private void Publish(float semitones) =>
        Interlocked.Exchange(ref _semBits, BitConverter.SingleToInt32Bits(semitones));

    private float Median(int n)
    {
        int start = (_head - n + WindowFrames) % WindowFrames;
        for (int i = 0; i < n; i++) _sortBuf[i] = _window[(start + i) % WindowFrames];
        var span = _sortBuf.AsSpan(0, n);
        span.Sort();
        return (n & 1) == 1 ? span[n / 2] : (span[n / 2 - 1] + span[n / 2]) * 0.5f;
    }
}
