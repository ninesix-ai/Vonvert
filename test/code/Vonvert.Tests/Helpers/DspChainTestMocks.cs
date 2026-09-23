// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Minimal IAudioEffect doubles shared by the DSPChain structural/exception
// tests. They live in the test assembly, so the reflection-based contract test
// (which enumerates the engine assembly's effects) does not pick them up.

namespace Vonvert.Tests.Helpers;

using Vonvert.Engine.DspEngine;

internal sealed class MockEffect : IAudioEffect
{
    public string Name { get; set; } = "Mock";
    public bool IsEnabled { get; set; } = true;

    /// <summary>Value written across the buffer, to observe chain order.</summary>
    public float MarkerValue { get; set; }

    public int ProcessCallCount { get; private set; }
    public int ResetCallCount { get; private set; }

    public void Process(System.Span<float> buffer)
    {
        ProcessCallCount++;
        for (int i = 0; i < buffer.Length; i++) buffer[i] = MarkerValue;
    }

    public void Reset() => ResetCallCount++;
}

internal sealed class ThrowingMockEffect : IAudioEffect
{
    public string Name { get; set; } = "Thrower";
    public bool IsEnabled { get; set; } = true;
    public int ProcessCallCount { get; private set; }

    /// <summary>Exception thrown by Process(); a generic one by default.</summary>
    public Exception ExceptionToThrow { get; set; } = new("mock DSP failure");

    public void Process(System.Span<float> buffer)
    {
        ProcessCallCount++;
        throw ExceptionToThrow;
    }

    public void Reset() { }
}
