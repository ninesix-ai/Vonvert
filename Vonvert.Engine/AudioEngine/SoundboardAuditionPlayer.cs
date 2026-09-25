// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Threading;

namespace Vonvert.Engine.AudioEngine;

/// <summary>Engine-independent local soundboard audition. Buffers one-shot PCM into a
/// <see cref="SoundboardMixer"/> and renders it on the default playback device via a
/// pluggable <see cref="IAudioSink"/>. Output is opened lazily and auto-released after a
/// short idle so a stopped audition never pins the device. Device failures degrade to a
/// logged warning; callers (the UI) are never interrupted.</summary>
public sealed class SoundboardAuditionPlayer : IAuditionPlayer, IDisposable
{
    private const int PollMs = 500;
    private const int StopTicks = 4;            // ~2 s of silence before release

    private readonly SoundboardMixer _mixer = new();
    private readonly Func<IAudioSink> _sinkFactory;
    private readonly object _lock = new();
    private IAudioSink? _sink;
    private Timer? _idleTimer;
    private int _silentTicks;

    public SoundboardAuditionPlayer() : this(mixer => new WaveOutAudioSink(mixer)) { }

    internal SoundboardAuditionPlayer(Func<SoundboardMixer, IAudioSink> sinkFactory)
        => _sinkFactory = () => sinkFactory(_mixer);

    /// <summary>Clips still being rendered (test/monitor seam).</summary>
    public int ActiveVoices => _mixer.ActiveVoices;

    public void Play(byte[] pcm, float volume)
    {
        if (pcm == null || pcm.Length == 0) return;
        _mixer.Enqueue(pcm, volume);
        lock (_lock)
        {
            _silentTicks = 0;
            EnsureStarted();
            _idleTimer ??= new Timer(OnIdleTick, null, PollMs, PollMs);
        }
    }

    private void EnsureStarted()
    {
        if (_sink != null) return;
        try
        {
            _sink = _sinkFactory();
            _sink.Start();
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[Audition] local output unavailable; audition disabled until next attempt");
            _sink = null;   // clips stay buffered; retried on next Play
        }
    }

    private void OnIdleTick(object? state)
    {
        lock (_lock)
        {
            if (_mixer.ActiveVoices == 0)
            {
                if (IdleDecider.ShouldStop(++_silentTicks, StopTicks))
                {
                    _idleTimer?.Dispose(); _idleTimer = null;
                    _sink?.Stop(); _sink?.Dispose(); _sink = null;
                }
            }
            else _silentTicks = 0;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _idleTimer?.Dispose(); _idleTimer = null;
            _sink?.Stop(); _sink?.Dispose(); _sink = null;
        }
        _mixer.Clear();
    }
}
