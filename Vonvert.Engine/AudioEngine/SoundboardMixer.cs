// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Mixes queued one-shot PCM (IEEE float32, mono, <see cref="AudioConstants.EngineRate"/>)
/// into the outgoing real-time work buffer AFTER the voice DSP chain. This keeps
/// soundboard clips un-pitched while still routing them through the render/monitor
/// output. Thread-safe: <see cref="Enqueue"/> runs on the UI thread,
/// <see cref="MixInto"/> on the DSP thread. A small fixed voice cap bounds overlap.
/// </summary>
public sealed class SoundboardMixer
{
    private readonly object _lock = new();
    private readonly List<(float[] Data, int Pos, float Vol)> _active = new();
    private const int MaxVoices = 8;

    /// <summary>Enqueue a clip for playback. <paramref name="pcm"/> is float32 mono @ EngineRate.</summary>
    public void Enqueue(byte[] pcm, float volume)
    {
        if (pcm == null || pcm.Length == 0) return;
        var samples = MemoryMarshal.Cast<byte, float>(pcm).ToArray();
        float v = Math.Clamp(volume, 0f, 1f);
        lock (_lock)
        {
            if (_active.Count < MaxVoices) _active.Add((samples, 0, v));
        }
    }

    /// <summary>Drop all pending playback (called on engine teardown).</summary>
    public void Clear()
    {
        lock (_lock) _active.Clear();
    }

    /// <summary>Number of clips currently still being rendered.</summary>
    public int ActiveVoices
    {
        get { lock (_lock) return _active.Count; }
    }

    /// <summary>
    /// Additively mix the active clips into <paramref name="work"/>, advancing each
    /// play cursor; finished clips are evicted. Output is clamped to [-1, 1].
    /// </summary>
    public void MixInto(Span<float> work)
    {
        lock (_lock)
        {
            for (int v = _active.Count - 1; v >= 0; v--)
            {
                var (data, pos, vol) = _active[v];
                for (int i = 0; i < work.Length && pos < data.Length; i++, pos++)
                {
                    float s = work[i] + data[pos] * vol;
                    work[i] = Math.Clamp(s, -1f, 1f);
                }
                if (pos >= data.Length) _active.RemoveAt(v);
                else _active[v] = (data, pos, vol);
            }
        }
    }
}
