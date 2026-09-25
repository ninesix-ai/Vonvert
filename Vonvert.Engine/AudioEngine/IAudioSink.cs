// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.Engine.AudioEngine;

/// <summary>Abstract render target the audition player drives; the real impl wraps
/// NAudio WaveOutEvent, tests inject an in-memory fake.</summary>
public interface IAudioSink : IDisposable
{
    void Start();
    void Stop();
}
