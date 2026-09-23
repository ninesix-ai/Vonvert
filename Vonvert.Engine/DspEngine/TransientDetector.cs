// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Runtime.CompilerServices;

namespace Vonvert.Engine.DspEngine;

/*
 * Real-time onset / transient detector for single-channel audio.
 *
 * Algorithm: log-energy flux + EMA z-score thresholding
 * (inspired by the MIT-licensed audiojs/pitch-shift transientThreshold).
 *
 *   ? Per-frame energy (~5 ms) → log domain
 *   ? Flux = positive-going delta of log-energy (only onsets count)
 *   ? EMA mean / variance track the background level
 *   ? Frame whose z-score exceeds DetectionLevel ? onset
 *   ? Output strength = clamp((z - thr) / rampRange, 0, 1)
 *     — a soft gate so crossfade mixes linearly instead of clicking
 *
 * All state lives in instance fields. Zero GC allocations per call.
 */
public sealed class TransientDetector
{
    // --- Tuning defaults (48 kHz) --------------------------------------------
    //
    //   AnalysisFrame 256  ≈  5.3 ms  →  fine enough to localise kick/snare
    //   SmoothAlpha ≈ 0.02  →  ~50-frame (~260 ms) envelope
    //   DetectionLevel 2.5  →  2.5 σ above background
    //   RampRange 2.0      →  z-score values from thr to thr+ramp map to 0→1
    //
    // Exposed as settable properties for runtime tuning.

    /// <summary>Samples per analysis frame. Default 256 ≈ 5.3 ms @ 48 kHz.</summary>
    public int AnalysisFrame
    {
        get => _analysisFrame;
        set
        {
            if (value < 64 || value > 4096)
                throw new ArgumentOutOfRangeException(nameof(value), "AnalysisFrame must be in [64, 4096].");
            _analysisFrame = value;
            Reset();
        }
    }

    /// <summary>EMA smoothing factor for mean/variance tracking. Default 0.02.</summary>
    public float SmoothAlpha
    {
        get => _smoothAlpha;
        set => _smoothAlpha = Math.Clamp(value, 0.001f, 0.5f);
    }

    /// <summary>z-score onset threshold. Higher = fewer (stronger) detections. Default 2.5.</summary>
    public float DetectionLevel
    {
        get => _detectionLevel;
        set => _detectionLevel = Math.Clamp(value, 1.0f, 6.0f);
    }

    /// <summary>z-score distance over which strength ramps 0→1. Default 2.0.</summary>
    public float RampRange
    {
        get => _rampRange;
        set => _rampRange = Math.Clamp(value, 0.5f, 6.0f);
    }

    // ── Internal state ──────────────────────────────────────────────────

    private int    _analysisFrame = 256;
    private float  _smoothAlpha   = 0.02f;
    private float  _detectionLevel = 2.5f;
    private float  _rampRange     = 2.0f;

    // Ongoing frame accumulator (we accept arbitrary-length Process() calls
    // so the detector doesn't force the caller into fixed-frame batching).
    private readonly float[] _analysisBuf;   // always length = max supported frame size
    private          int     _analysisFill;  // how many samples of the current frame we have
    private          float   _prevEnergy;
    private          float   _bgMean;
    private          float   _bgVar;

    // Last frame's strength — used to sub-sample smooth so a single-frame
    // detection doesn't produce a 1-sample-wide step in the crossfade envelope.
    private          float   _prevStrength;
    // Current frame's target strength — interpolate across the next frame.
    private          float   _targetStrength;
    // Progress through the interpolation (0..frameSize-1).
    private          int     _interpCursor;

    // Maximum supported frame size — _analysisBuf is allocated once at this size
    // so shrinking/growing AnalysisFrame at runtime doesn't re-alloc. 4096 = 85 ms
    // @ 48 kHz; anything larger is for offline analysis only.
    private const int MaxFrameSize = 4096;

    // Small floor so log(0) doesn't -inf on digital silence.
    private const float Floor = 1e-12f;

    public TransientDetector()
    {
        _analysisBuf = new float[MaxFrameSize];
        Reset();
    }

    /// <summary>Reset all running state to cold-start defaults.</summary>
    public void Reset()
    {
        Array.Clear(_analysisBuf, 0, _analysisBuf.Length);
        _analysisFill    = 0;
        _prevEnergy      = MathF.Log(Floor);
        _bgMean          = 0f;
        _bgVar           = 1f;  // start with unit variance so initial z-scores are sane
        _prevStrength    = 0f;
        _targetStrength  = 0f;
        _interpCursor    = 0;
    }

    /// <summary>
    /// Process a mono buffer in-place, writing per-sample transient strength
    /// (0.0 = no transient .. 1.0 = very strong transient) into
    /// <paramref name="strengthsOut"/>.
    /// </summary>
    /// <returns>Maximum strength observed in this call.</returns>
    public float Process(ReadOnlySpan<float> input, Span<float> strengthsOut)
    {
        if (input.Length != strengthsOut.Length)
            throw new ArgumentException("input and strengthsOut must be the same length.", nameof(strengthsOut));

        float peak = 0f;
        int   n    = input.Length;
        int   k    = 0;

        while (k < n)
        {
            // 1. Drain the interpolation ramp — between frames we linearly
            //    interpolate strength so the crossfade envelope stays smooth.
            int toDrain = Math.Min(n - k, _analysisFrame - _interpCursor);
            if (toDrain > 0)
            {
                float from = _prevStrength;
                float to   = _targetStrength;
                float step = _analysisFrame > 1 ? 1f / _analysisFrame : 1f;
                for (int i = 0; i < toDrain; i++)
                {
                    float t = (_interpCursor + i) * step;
                    float s = from + (to - from) * t;
                    if (s > peak) peak = s;
                    strengthsOut[k + i] = s;
                }
                _interpCursor += toDrain;
                k             += toDrain;
                if (_interpCursor >= _analysisFrame)
                {
                    _prevStrength = _targetStrength;
                    _interpCursor = 0;
                }
            }

            // 2. Accumulate input samples into the analysis frame
            int room = _analysisFrame - _analysisFill;
            int take = Math.Min(n - k, room);
            if (take > 0)
            {
                input.Slice(k, take).CopyTo(_analysisBuf.AsSpan(_analysisFill, take));
                _analysisFill += take;
                k             += take;
            }

            // 3. Full frame ? run the detector step
            if (_analysisFill >= _analysisFrame)
            {
                _targetStrength = AnalyzeFrame(_analysisBuf.AsSpan(0, _analysisFrame));
                _analysisFill   = 0;
            }
        }

        return peak;
    }

    /// <summary>
    /// Core detector math — runs once per analysis frame.
    /// Exposed as internal for unit-test access (<c>InternalsVisibleTo</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float AnalyzeFrame(ReadOnlySpan<float> frame)
    {
        // a) Mean frame energy → log domain.
        float sum = 0f;
        foreach (float s in frame)
            sum += s * s;
        float energy    = sum / frame.Length;
        float logEnergy = MathF.Log(energy + Floor);

        // b) Positive-only flux — an onset is an *increase* in log energy.
        float flux = MathF.Max(0f, logEnergy - _prevEnergy);
        _prevEnergy = logEnergy;

        // c) EMA mean / variance (Welford-style incremental update).
        float alpha = _smoothAlpha;
        float delta = flux - _bgMean;
        _bgMean += alpha * delta;
        // Var update uses the *previous* mean (matches standard EMA variance form).
        _bgVar = (1f - alpha) * (_bgVar + alpha * delta * delta);
        if (_bgVar < 1e-10f) _bgVar = 1e-10f;

        // d) z-score → soft strength in [0, 1].
        float stdDev = MathF.Sqrt(_bgVar);
        float z      = (flux - _bgMean) / (stdDev + 1e-8f);
        float thr    = _detectionLevel;
        float width  = _rampRange;
        float strength = (z - thr) / width;
        if (strength < 0f) strength = 0f;
        if (strength > 1f) strength = 1f;

        return strength;
    }
}
