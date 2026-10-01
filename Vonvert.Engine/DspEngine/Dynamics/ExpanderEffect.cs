// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Dynamics;

/*
 * Expander / Upward-Downward Expander — dynamic range processor that
 * reduces gain when the signal falls below the threshold.
 *
 * Unlike a noise gate (which hard-mutes below threshold), an expander
 * applies smooth, proportional gain reduction — more natural for
 * cleaning up background noise, room tone, or amp hiss without the
 * abrupt on/off character of a gate.
 *
 * Algorithm:
 *   1. Track the input envelope with separate attack/release smoothing.
 *   2. Compare envelope against the threshold:
 *      - Above threshold: unity gain (no expansion).
 *      - Below threshold: gain reduction proportional to the ratio.
 *        e.g. 2:1 ratio → signal drops 2 dB for every 1 dB below thresh.
 *   3. Range parameter sets the maximum gain reduction (floor).
 *   4. Apply smoothed gain to avoid clicks.
 *
 * Use cases:
 *   - Smoother alternative to NoiseGate for voice noise cleanup
 *   - Controlling room ambience between vocal phrases
 *   - "Expander" feel on drums / percussive voice
 *
 * All state pre-allocated. Zero GC on DSP thread.
 */
public sealed class ExpanderEffect : IAudioEffect
{
    public string Name      { get; } = "Expander";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _thresholdDb = -30f;
    private float _ratio       = 2f;
    private float _attackMs    = 1f;
    private float _releaseMs   = 50f;
    private float _rangeDb     = -40f;

    /// <summary>Expansion threshold in dB (-80 to 0). Default: -30.</summary>
    public float ThresholdDb
    {
        get => _thresholdDb;
        set { _thresholdDb = Math.Clamp(value, -80f, 0f); _threshLin = VonvertUtils.DbToLinear(value); }
    }

    /// <summary>Expansion ratio (1 = transparent, 20 = aggressive). Default: 2.</summary>
    public float Ratio
    {
        get => _ratio;
        set => _ratio = Math.Clamp(value, 1f, 20f);
    }

    /// <summary>Attack time in ms (0.1 – 50). Default: 1.</summary>
    public float AttackMs
    {
        get => _attackMs;
        set => _attackMs = Math.Clamp(value, 0.1f, 50f);
    }

    /// <summary>Release time in ms (1 – 1000). Default: 50.</summary>
    public float ReleaseMs
    {
        get => _releaseMs;
        set => _releaseMs = Math.Clamp(value, 1f, 1000f);
    }

    /// <summary>Maximum gain reduction in dB (-80 to 0). Default: -40.</summary>
    public float RangeDb
    {
        get => _rangeDb;
        set => _rangeDb = Math.Clamp(value, -80f, 0f);
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const float FS = 48000f;

    private float _threshLin = VonvertUtils.DbToLinear(-30f);
    private float _envelope  = 0f;
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

            // Envelope follower (peak-hold with attack/release)
            _envelope = mag > _envelope
                ? atkCoef * (_envelope - mag) + mag
                : relCoef * (_envelope - mag) + mag;

            // Compute expander gain
            float gain;
            if (_envelope > thresh)
            {
                gain = 1f; // above threshold — no expansion
            }
            else if (_envelope > 1e-9f)
            {
                // Below threshold: apply expansion ratio
                float envDb    = VonvertUtils.LinearToDb(_envelope);
                float threshDb = VonvertUtils.LinearToDb(thresh);
                float overDb   = threshDb - envDb;           // positive value
                float reduceDb = overDb * (ratio - 1f);      // gain reduction in dB
                gain = VonvertUtils.DbToLinear(-reduceDb);
                gain = MathF.Max(gain, rangeLin);             // clamp to range floor
            }
            else
            {
                gain = rangeLin; // silence → maximum reduction
            }

            // Smooth the gain to avoid clicks
            _smoothGain = gain > _smoothGain
                ? relCoef * (_smoothGain - gain) + gain   // opening → release time
                : atkCoef * (_smoothGain - gain) + gain;   // closing  → attack time

            buf[i] *= _smoothGain;
        }
    }

    public void Reset()
    {
        _envelope   = 0f;
        _smoothGain = 1f;
    }
}
