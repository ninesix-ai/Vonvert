// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Pitch analysis output contract.
/// Implemented by <see cref="PitchAnalyzer"/>.
/// </summary>
public interface IPitchAnalyzer
{
    PitchSnapshot GetSnapshot();
    float[] GetHistory();
    int GetHistoryCount();
    void Reset();
}
