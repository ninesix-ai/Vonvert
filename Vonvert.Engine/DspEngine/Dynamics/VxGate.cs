// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Dynamics;

/*
 * Hysteresis noise gate with hold phase.
 *
 * Unlike a simple threshold gate, this design uses two thresholds
 * (open / close) to prevent chattering at the boundary, and a hold
 * counter that keeps the gate open for a configurable duration after
 * the signal drops below the close threshold.
 *
 * State machine:
 *   CLOSED  ──(peak ≥ openThreshold)──▶  OPEN
 *   OPEN    ──(peak < closeThreshold)──▶ HOLD
 *   HOLD    ──(holdCount ≥ holdSamples)──▶ CLOSED
 *         ──(peak ≥ openThreshold)──────▶ OPEN
 */
public sealed class VxGate : IAudioEffect
{
    public string Name => "VoiceGate";
    public bool   IsEnabled { get; set; } = true;

    // ── Parameters ──────────────────────────────────────────────────
    public float ThresholdDb { get; set; } = -45f;
    public float AttackMs    { get; set; } = 2f;
    public float ReleaseMs   { get; set; } = 100f;
    public float HoldMs      { get; set; } = 50f;

    // ── Internal state ──────────────────────────────────────────────
    private const int SampleRate = 48_000;
    private const float HysteresisDb = 3f;       // gap between open/close thresholds

    private enum GateState { Closed, Open, Hold }
    private GateState _state = GateState.Closed;

    private float _smoothGain;
    private int   _holdCounter;

    public void Process(Span<float> block)
    {
        var openLin  = System.MathF.Pow(10f, ThresholdDb * 0.05f);
        var closeLin = System.MathF.Pow(10f, (ThresholdDb - HysteresisDb) * 0.05f);
        var attackCoef  = System.MathF.Exp(-1f / (AttackMs  * 0.001f * SampleRate));
        var releaseCoef = System.MathF.Exp(-1f / (ReleaseMs * 0.001f * SampleRate));
        var holdSamples = (int)(HoldMs * 0.001f * SampleRate);

        for (var idx = 0; idx < block.Length; ++idx)
        {
            var peak = System.MathF.Abs(block[idx]);

            switch (_state)
            {
                case GateState.Closed:
                    if (peak >= openLin)
                    {
                        _state = GateState.Open;
                        _holdCounter = holdSamples;
                    }
                    break;

                case GateState.Open:
                    if (peak < closeLin)
                    {
                        _state = GateState.Hold;
                        _holdCounter = holdSamples;
                    }
                    break;

                case GateState.Hold:
                    if (peak >= openLin)
                    {
                        _state = GateState.Open;
                        _holdCounter = holdSamples;
                    }
                    else if (--_holdCounter <= 0)
                    {
                        _state = GateState.Closed;
                    }
                    break;
            }

            var target = _state == GateState.Closed ? 0f : 1f;
            var coef   = target > _smoothGain ? attackCoef : releaseCoef;
            _smoothGain = coef * (_smoothGain - target) + target;

            block[idx] *= _smoothGain;
        }
    }

    public void Reset()
    {
        _smoothGain  = 0f;
        _holdCounter = 0;
        _state       = GateState.Closed;
    }
}
