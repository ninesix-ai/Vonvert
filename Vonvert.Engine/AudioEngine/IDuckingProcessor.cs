// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Sidechain ducking processor contract.
/// Implemented by <see cref="DuckingProcessor"/>.
/// </summary>
public interface IDuckingProcessor
{
    bool IsEnabled { get; set; }
    float ThresholdDb { get; set; }
    float Ratio { get; set; }
    float Depth { get; set; }
}
