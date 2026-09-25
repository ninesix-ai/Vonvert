// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Vonvert.Engine.AudioEngine;

/// <summary>Continuous IEEE-float mono stream at EngineRate: silence plus the mixer's
/// active one-shots. WaveOut pulls from Read on its own thread.</summary>
public sealed class AuditionWaveProvider : IWaveProvider
{
    private readonly SoundboardMixer _mixer;
    public WaveFormat WaveFormat { get; }

    public AuditionWaveProvider(SoundboardMixer mixer)
    {
        _mixer = mixer;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AudioConstants.EngineRate, 1);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var dest = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, count));
        dest.Clear();            // silence floor
        _mixer.MixInto(dest);    // layer any active clips, evict finished ones
        return count;
    }
}

/// <summary>Real sink: WaveOutEvent on the default playback endpoint.</summary>
public sealed class WaveOutAudioSink : IAudioSink
{
    private readonly WaveOutEvent _out = new();
    private readonly AuditionWaveProvider _provider;

    public WaveOutAudioSink(SoundboardMixer mixer)
    {
        _provider = new AuditionWaveProvider(mixer);
        _out.Init(_provider);   // throws if no render device — caller guards
    }

    public void Start() => _out.Play();
    public void Stop()  => _out.Stop();
    public void Dispose()
    {
        try { _out.Stop(); } catch { }
        try { _out.Dispose(); } catch { }
    }
}
