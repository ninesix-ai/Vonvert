// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Mixes queued one-shot PCM (IEEE float32, mono, <see cref="AudioConstants.EngineRate"/>)
/// additively into a real-time work buffer, AFTER the voice DSP chain so clips stay
/// un-pitched. Thread-safe: <see cref="Enqueue"/> runs on the UI thread,
/// <see cref="MixInto"/> on a render thread. A fixed voice cap bounds overlap.
/// Each enqueue returns a monotonically increasing token; re-enqueuing the same
/// <c>soundId</c> replaces that pad's previous voice so progress UIs restart cleanly.
/// </summary>
public sealed class SoundboardMixer
{
    private sealed class Voice
    {
        public readonly long Token;
        public readonly string? SoundId;
        public readonly float[] Data;
        public int Pos;
        public readonly float Vol;
        public Voice(long token, string? soundId, float[] data, float vol)
        { Token = token; SoundId = soundId; Data = data; Vol = vol; }
    }

    private readonly object _lock = new();
    private readonly List<Voice> _active = new();
    private long _nextToken = 1;
    private const int MaxVoices = 8;

    /// <summary>Enqueue a clip for playback; returns its tracking token (0 if ignored).
    /// A non-null <paramref name="soundId"/> replaces that pad's previous voice.</summary>
    public long Enqueue(byte[] pcm, float volume, string? soundId = null)
    {
        if (pcm == null || pcm.Length == 0) return 0;
        var samples = MemoryMarshal.Cast<byte, float>(pcm).ToArray();
        float v = Math.Clamp(volume, 0f, 1f);
        long token;
        lock (_lock)
        {
            token = _nextToken++;
            if (soundId != null)
                _active.RemoveAll(voice => voice.SoundId == soundId);
            if (_active.Count >= MaxVoices) _active.RemoveAt(0);
            _active.Add(new Voice(token, soundId, samples, v));
        }
        return token;
    }

    /// <summary>Read-only progress for one token. False once finished, evicted or unknown.</summary>
    public bool TryGetProgress(long token, out int position, out int length)
    {
        lock (_lock)
        {
            var voice = _active.Find(v => v.Token == token);
            if (voice == null) { position = 0; length = 0; return false; }
            position = voice.Pos;
            length = voice.Data.Length;
            return true;
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
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var voice = _active[i];
                int n = 0;
                for (; n < work.Length && voice.Pos < voice.Data.Length; n++, voice.Pos++)
                {
                    float s = work[n] + voice.Data[voice.Pos] * voice.Vol;
                    work[n] = Math.Clamp(s, -1f, 1f);
                }
                if (voice.Pos >= voice.Data.Length) _active.RemoveAt(i);
            }
        }
    }
}
