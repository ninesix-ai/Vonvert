// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.Engine.DspEngine.Pitch;

/*
 * Polyphonic pitch shifter with a pluggable backend and transient protection.
 *
 * Default backend: hand-written SOLA (open-source, zero dependencies).
 * Alternative backends can be injected via SetBackend().
 *
 * Transient protection: when enabled, the built-in TransientDetector analyses
 *   the input for onset events (drums, plosives). During transients the output
 *   crossfades toward the dry signal, preventing pitch-shift smearing artifacts.
 *
 * Range: -24 .. +24 semitones (clamped).
 */
public sealed class VxPitch : IAudioEffect
{
    public string Name      { get; } = "VoicePitch";
    public bool   IsEnabled { get; set; }

    private float _pitch = 0f;
    public float Semitones
    {
        get => _pitch;
        set
        {
            _pitch = Math.Clamp(value, -24f, 24f);
            _ratio = MathF.Pow(2f, _pitch / 12f);
            if (_backend is not null)
                _backend.PitchFactor = _ratio;
        }
    }

    // ── Transient protection ─────────────────────────────────────────────

    private readonly TransientDetector _transientDet = new();
    private float[] _strengthBuf = new float[4096]; // pre-allocated, resized on demand
    private float[] _dryBuf      = new float[4096]; // pre-allocated dry signal for transient crossfade

    /// <summary>Enable transient-aware pitch shifting. When true, onset
    /// events (drums, plosives) blend toward the dry signal to avoid
    /// pitch-shift smearing. Default false.</summary>
    public bool TransientProtection
    {
        get => _transientProtection;
        set => _transientProtection = value;
    }
    private bool _transientProtection = false;

    /// <summary>Access the underlying TransientDetector for parameter tuning.
    /// Defaults: AnalysisFrame=256, DetectionLevel=2.5, RampRange=2.0.</summary>
    public TransientDetector TransientDetector => _transientDet;

    // ── Backend injection ────────────────────────────────────────────────

    private IPitchBackend? _backend;
    private bool _idle = true;

    /// <summary>Inject a custom pitch-shifting backend.
    /// Pass <c>null</c> to fall back to the built-in SOLA implementation.</summary>
    public void SetBackend(IPitchBackend? backend)
    {
        _backend = backend;
        if (_backend is not null)
            _backend.PitchFactor = MathF.Pow(2f, _pitch / 12f);
        Reset();
    }

    /// <summary>Currently injected backend — null means the built-in SOLA path
    /// is active. Internal observation point for quality-mode tests
    /// (InternalsVisibleTo Vonvert.Tests).</summary>
    internal IPitchBackend? Backend => _backend;

    // ── Auto-pitch closed-loop source (optional) ───────────────────────

    /// <summary>Optional closed-loop source that overrides the manual Semitones
    /// when <see cref="AutoTargetEnabled"/> is set and the source is ready.
    /// Read-only on the audio thread; never mutate VxPitch from the producer.</summary>
    public Vonvert.Engine.AudioEngine.IAutoPitchSource? AutoSource { get; set; }
    public bool AutoTargetEnabled { get; set; }

    // ── Built-in SOLA (open-source fallback) ─────────────────────────────

    private float _ratio = 1f;

    private const int WINDOW = 2048;
    private const int HOP    = 1024;
    private const int IN_CAP   = 8192;
    private const int IN_MASK  = IN_CAP  - 1;
    private const int OUT_CAP  = 32768; // 4x input cap — prevents grain overlap at high ratios
    private const int OUT_MASK = OUT_CAP - 1;

    private readonly float[] _inRing  = new float[IN_CAP];
    private readonly float[] _outRing = new float[OUT_CAP];
    private readonly float[] _hann    = BuildHannWindow(WINDOW);

    private int  _inWritePos  = 0;
    private int  _nextWindow  = 0;

    // ── IAudioEffect ─────────────────────────────────────────────────────

    public void Process(Span<float> buf)
    {
        float eff    = (AutoTargetEnabled && AutoSource is { Enabled: true, HasTarget: true } src)
                       ? src.Semitones : _pitch;
        float ratio  = MathF.Pow(2f, eff / 12f);
        bool  bypass = MathF.Abs(ratio - 1f) < 0.001f;

        // ── Transient detection (runs on dry signal before pitch shift) ──
        bool hasDry = false;
        if (TransientProtection && !bypass)
        {
            // Ensure buffers are large enough (pre-allocated, no per-block GC)
            if (_strengthBuf.Length < buf.Length)
                _strengthBuf = new float[buf.Length];
            if (_dryBuf.Length < buf.Length)
                _dryBuf = new float[buf.Length];
            var strengthSpan = _strengthBuf.AsSpan(0, buf.Length);

            // Save dry signal for crossfade after pitch shift
            buf.CopyTo(_dryBuf.AsSpan(0, buf.Length));
            hasDry = true;

            // Run transient detector
            _transientDet.Process(buf, strengthSpan);
        }

        if (_backend is not null)
        {
            // ── External backend path ────────────────────────────────────
            if (bypass)
            {
                if (!_idle) { _backend.Reset(); _idle = true; }
            }
            else
            {
                if (_idle) _idle = false;
                _backend.PitchFactor = ratio;
                _backend.ProcessInPlace(buf);
            }
        }
        else
        {
            // ── Built-in SOLA path ───────────────────────────────────────
            if (bypass) return;
            ProcessSola(buf, ratio);
        }

        // ── Transient crossfade: blend dry into wet during onsets ────────
        if (hasDry)
        {
            var strengthSpan = _strengthBuf.AsSpan(0, buf.Length);
            var drySpan = _dryBuf.AsSpan(0, buf.Length);
            for (int i = 0; i < buf.Length; i++)
            {
                float s = strengthSpan[i]; // 0=no transient, 1=strong transient
                if (s > 0.001f)
                    buf[i] = drySpan[i] * s + buf[i] * (1f - s);
            }
        }
    }

    public void Reset()
    {
        _transientDet.Reset();
        if (_backend is not null)
        {
            _backend.Reset();
            _idle = true;
        }
        else
        {
            Array.Clear(_inRing);
            Array.Clear(_outRing);
            _inWritePos = 0;
            _nextWindow = 0;
        }
    }

    // ── SOLA implementation ──────────────────────────────────────────────

    private void ProcessSola(Span<float> buf, float r)
    {
        int readAhead = (int)MathF.Ceiling(WINDOW * MathF.Max(1f, r)) + 2;
        int outLag    = readAhead + 1;

        for (int i = 0; i < buf.Length; i++)
        {
            _inRing[_inWritePos & IN_MASK] = buf[i];
            _inWritePos++;

            while (_inWritePos - _nextWindow >= readAhead)
            {
                ExtractWindow(_nextWindow, r);
                _nextWindow += HOP;
            }

            int outPos = _inWritePos - outLag;
            if (outPos < 0)
            {
                buf[i] = 0f;
            }
            else
            {
                int idx = outPos & OUT_MASK;
                buf[i] = _outRing[idx];
                _outRing[idx] = 0f;
            }
        }
    }

    private void ExtractWindow(int pos, float r)
    {
        for (int k = 0; k < WINDOW; k++)
        {
            float inPos = pos + k * r;
            int   lo    = (int)inPos;
            int   hi    = lo + 1;
            float frac  = inPos - lo;
            float s     = _inRing[lo & IN_MASK] * (1f - frac) + _inRing[hi & IN_MASK] * frac;
            _outRing[(pos + k) & OUT_MASK] += s * _hann[k];
        }
    }

    private static float[] BuildHannWindow(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / n));
        return w;
    }
}
