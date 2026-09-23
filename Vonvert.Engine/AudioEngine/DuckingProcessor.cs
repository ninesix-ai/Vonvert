// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Sidechain ducking processor — automatically reduces BGM volume when voice is detected.
/// Uses envelope follower to track mic input level and apply smooth gain reduction to BGM.
/// </summary>
public sealed class DuckingProcessor : IDuckingProcessor
{
    public bool IsEnabled { get; set; } = false;

    /// <summary>Mic level threshold (dB) to trigger ducking. Default -30 dB.</summary>
    public float ThresholdDb { get; set; } = -30f;

    /// <summary>Compression ratio (e.g. 4 = 4:1). Higher = more ducking. Default 4.</summary>
    public float Ratio { get; set; } = 4f;

    /// <summary>Attack time (ms) — how fast ducking engages. Default 10 ms.</summary>
    public float AttackMs { get; set; } = 10f;

    /// <summary>Release time (ms) — how fast ducking disengages. Default 100 ms.</summary>
    public float ReleaseMs { get; set; } = 100f;

    /// <summary>Maximum ducking depth (0..1). 0 = no ducking, 1 = full mute. Default 0.8.</summary>
    public float Depth { get; set; } = 0.8f;

    // --- Envelope follower state (DSP thread only) ------------------------
    private float _envelope = 0f;
    private float _attackCoef;
    private float _releaseCoef;
    private float _currentGain = 1f;

    private const int SAMPLE_RATE = 48000;

    public DuckingProcessor()
    {
        UpdateCoefficients();
    }

    /// <summary>Update attack/release coefficients when parameters change.</summary>
    private void UpdateCoefficients()
    {
        _attackCoef  = MathF.Exp(-1f / (AttackMs  * 0.001f * SAMPLE_RATE));
        _releaseCoef = MathF.Exp(-1f / (ReleaseMs * 0.001f * SAMPLE_RATE));
    }

    /// <summary>
    /// Analyze mic buffer and compute ducking gain, then apply to BGM buffer.
    /// Called from DSP thread. Zero allocation on hot path.
    /// </summary>
    /// <param name="micBuffer">Mic input samples (analyzed but not modified).</param>
    /// <param name="bgmBuffer">BGM samples (modified in-place with ducking gain).</param>
    public void Process(ReadOnlySpan<float> micBuffer, Span<float> bgmBuffer)
    {
        if (!IsEnabled) return;

        UpdateCoefficients();
        float thresh = MathF.Pow(10f, ThresholdDb / 20f);

        int len = Math.Min(micBuffer.Length, bgmBuffer.Length);

        for (int i = 0; i < len; i++)
        {
            // 1. Track mic envelope
            float mag = MathF.Abs(micBuffer[i]);
            _envelope = mag > _envelope
                ? _attackCoef * (_envelope - mag) + mag
                : _releaseCoef * (_envelope - mag) + mag;

            // 2. Compute gain reduction
            float targetGain = 1f;
            if (_envelope > thresh)
            {
                float excess = _envelope - thresh;
                float compressed = excess / Ratio;
                float gr = thresh + compressed;
                targetGain = gr / (_envelope + 1e-9f);
                // Apply depth: interpolate between 1.0 and targetGain
                targetGain = 1f - (1f - targetGain) * Depth;
            }

            // 3. Smooth gain transition (avoid clicks)
            float gainStep = targetGain > _currentGain ? _attackCoef : _releaseCoef;
            _currentGain = gainStep * (_currentGain - targetGain) + targetGain;

            // 4. Apply to BGM
            bgmBuffer[i] *= _currentGain;
        }
    }

    /// <summary>Reset envelope state.</summary>
    public void Reset()
    {
        _envelope = 0f;
        _currentGain = 1f;
    }
}
