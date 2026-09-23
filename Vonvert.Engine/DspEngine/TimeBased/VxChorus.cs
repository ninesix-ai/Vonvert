// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine.TimeBased;

/*
 * Triple-LFO chorus unit.
 *
 * Three sinusoidal LFOs run at slightly different rates (1×, 1.33×, 0.71×)
 * and 120° apart in phase.  Each LFO reads from a shared circular delay
 * line at a different fractional position; the three voices are summed
 * and mixed with the dry signal.
 *
 * This produces a richer, more "ensemble-like" modulation than a single
 * LFO design, with sub-sample interpolation eliminating quantisation noise.
 */
public sealed class VxChorus : IAudioEffect
{
    public string Name      { get; } = "VoiceChorus";
    public bool   IsEnabled { get; set; }

    // ── Parameters ──────────────────────────────────────────────────
    public float Rate  { get; set; } = 1.6f;   // base LFO Hz
    public float Depth { get; set; } = 0.3f;   // modulation depth 0–1
    public float Mix   { get; set; } = 0.4f;   // wet/dry 0–1

    // ── Delay line (power-of-2 for fast wrap) ───────────────────────
    private const int LineLen  = 5120;           // ~106 ms @ 48 kHz
    private const int LineMask = LineLen - 1;
    private readonly float[] _line = new float[LineLen];
    private int _writeIdx;

    // ── Triple-LFO state ────────────────────────────────────────────
    // Rate multipliers and fixed phase offsets (120° apart)
    private static readonly float[] RateMult   = [1.00f, 1.33f, 0.71f];
    private static readonly float[] PhaseBias  = [0.00f, 0.333f, 0.667f];
    private const int VoiceCount = 3;
    private readonly float[] _lfoPhase = new float[VoiceCount];

    public void Process(Span<float> buf)
    {
        var baseInc   = Rate / AudioConstants.EngineRate;
        var wetWeight = Mix;
        var dryWeight = 1f - wetWeight;
        var invVoices = 1f / VoiceCount;

        for (var i = 0; i < buf.Length; i++)
        {
            // Write current sample into the delay line
            _line[_writeIdx] = buf[i];

            // Sum contributions from all three LFO voices
            float voiceSum = 0f;
            for (var v = 0; v < VoiceCount; v++)
            {
                var effectivePhase = (_lfoPhase[v] + PhaseBias[v]) % 1f;
                var modOffset      = SineLookup(effectivePhase) * Depth * 480f + 48f;
                voiceSum += ReadFractional(modOffset);

                // Advance this LFO
                _lfoPhase[v] = (_lfoPhase[v] + baseInc * RateMult[v]) % 1f;
            }

            buf[i] = buf[i] * dryWeight + voiceSum * invVoices * wetWeight;
            _writeIdx = (_writeIdx + 1) & LineMask;
        }
    }

    public void Reset()
    {
        System.Array.Clear(_line);
        System.Array.Clear(_lfoPhase);
        _writeIdx = 0;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    // Read from the delay line at a fractional sample offset
    private float ReadFractional(float offsetSamples)
    {
        var intPart  = (int)offsetSamples;
        var fracPart = offsetSamples - intPart;

        var idx0 = (_writeIdx - intPart     + LineLen) & LineMask;
        var idx1 = (_writeIdx - intPart - 1 + LineLen) & LineMask;

        return _line[idx0] + fracPart * (_line[idx1] - _line[idx0]);
    }

    // Fast sine approximation via MathF (could be replaced with a LUT)
    private static float SineLookup(float phase)
        => 0.5f + 0.5f * System.MathF.Sin(System.MathF.Tau * phase);
}
