// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine.Pitch;

public interface IPitchBackend
{
    float PitchFactor { get; set; }
    int ProcessInPlace(Span<float> buf);
    void Reset();
}
