// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Waveform data provider for UI visualisation.
/// Implemented by <see cref="WaveformCapture"/>.
/// </summary>
public interface IWaveformCapture
{
    float[] GetWaveform();
    float GetPeak();
    void Reset();
}
