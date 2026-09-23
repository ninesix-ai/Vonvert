// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Real-time audio processing contract.
 *
 * Every node in the Vonvert DSP graph implements this interface.
 * The real-time audio thread calls Process() for each buffer —
 * implementations MUST NOT allocate heap memory, acquire locks,
 * or throw exceptions.
 *
 * Lifecycle:
 *   1. Engine creates the effect and sets parameters.
 *   2. Process() is called repeatedly on the audio thread.
 *   3. Reset() is called when the audio device changes or the
 *      engine is restarted, to clear internal delay-line state.
 */
public interface IAudioEffect
{
    /// <summary>Short identifier shown in the DSP chain UI.</summary>
    string Name { get; }

    /// <summary>When false the engine skips calling Process() entirely.</summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// In-place audio processing.  Buffer is 48 kHz mono float32.
    /// Must complete within the audio callback deadline.
    /// </summary>
    void Process(Span<float> buffer);

    /// <summary>
    /// Clear all internal state (delay lines, envelope followers, etc.).
    /// Called on device switch or engine restart.
    /// </summary>
    void Reset();
}
