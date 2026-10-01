// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Dynamics;

/*
 * Ducking Compressor — automatic gain reduction triggered by the input
 * signal's own amplitude. Creates "mix space" for voice by lowering
 * background level when speech is detected.
 *
 * In a full sidechain implementation, an external signal would drive
 * the gain reduction. In this mono-chain variant, the effect analyses
 * the input buffer itself: when voice-level energy is detected, the
 * overall gain is smoothly reduced (ducked), then released when the
 * voice stops.
 *
 * Algorithm:
 *   1. Track input envelope with fast attack / slow release.
 *   2. When envelope exceeds threshold, compute gain reduction.
 *   3. Apply smoothed gain reduction to the entire signal.
 *   4. Range parameter limits maximum ducking depth.
 *
 * Use cases:
 *   - Auto-duck background music under voice
 *   - "Broadcast" voice-over effect
 *   - Dynamic background noise reduction
 *
 * All state pre-allocated. Zero GC on DSP thread.
 */
public sealed class VxDuckingCompressor : IAudioEffect
{
    public string Name      { get; } = "DuckingCompressor";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _thresholdDb = -20f;
    private float _ratio       = 4f;
    private float _attackMs    = 2f;
    private float _releaseMs   = 200f;
    private float _rangeDb     = -20f;

    /// <summary>Ducking threshold in dB (-60 to 0). Default: -20.</summary>
    public float ThresholdDb
    {
        get => _thresholdDb;
        set { _thresholdDb = Math.Clamp(value, -60f, 0f); _threshLin = VonvertUtils.DbToLinear(value); }
    }

    /// <summary>Ducking ratio (1 – 20). Higher = deeper ducking. Default: 4.</summary>
    public float Ratio
    {
        get => _ratio;
        set => _ratio = Math.Clamp(value, 1f, 20f);
    }

    /// <summary>Attack time in ms (0.5 – 50). Default: 2.</summary>
    public float AttackMs
    {
        get => _attackMs;
        set => _attackMs = Math.Clamp(value, 0.5f, 50f);
    }

    /// <summary>Release time in ms (50 – 2000). Default: 200.</summary>
    public float ReleaseMs
    {
        get => _releaseMs;
        set => _releaseMs = Math.Clamp(value, 50f, 2000f);
    }

    /// <summary>Maximum ducking depth in dB (-60 to 0). Default: -20.</summary>
    public float RangeDb
    {
        get => _rangeDb;
        set => _rangeDb = Math.Clamp(value, -60f, 0f);
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const float FS = 48000f;

    private float _threshLin = VonvertUtils.DbToLinear(-20f);
    private float _envelope   = 0f;
    private float _smoothGain = 1f;

    public void Process(Span<float> buf)
    {
        float thresh  = _threshLin;
        float ratio   = _ratio;
        float rangeLin = VonvertUtils.DbToLinear(_rangeDb);
        float atkCoef = MathF.Exp(-1f / (_attackMs  / 1000f * FS));
        float relCoef = MathF.Exp(-1f / (_releaseMs / 1000f * FS));

        for (int i = 0; i < buf.Length; i++)
        {
            float mag = MathF.Abs(buf[i]);

            // Fast attack, slow release envelope follower
            _envelope = mag > _envelope
                ? atkCoef * (_envelope - mag) + mag
                : relCoef * (_envelope - mag) + mag;

            // Compute ducking gain
            float targetGain;
            if (_envelope > thresh && _envelope > 1e-9f)
            {
                float envDb    = VonvertUtils.LinearToDb(_envelope);
                float threshDb = VonvertUtils.LinearToDb(thresh);
                float overDb   = envDb - threshDb;
                float reduceDb = overDb * (ratio - 1f);
                targetGain = VonvertUtils.DbToLinear(-reduceDb);
                targetGain = MathF.Max(targetGain, rangeLin);
            }
            else
            {
                targetGain = 1f;
            }

            // Smooth the gain (ducking uses slow release for natural fade)
            _smoothGain = targetGain > _smoothGain
                ? relCoef * (_smoothGain - targetGain) + targetGain
                : atkCoef * (_smoothGain - targetGain) + targetGain;

            buf[i] *= _smoothGain;
        }
    }

    public void Reset()
    {
        _envelope   = 0f;
        _smoothGain = 1f;
    }
}
