// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.IO;
using NAudio.Wave;
using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

// ═══════════════════════════════════════════════════════════════════
//  BackgroundMusicPlayer — the BGM transport that feeds resampled mono
//  frames to the post-DSP mix in VoiceEngine. These tests drive it at
//  the engine rate (48 kHz / mono) so the sample mapping is 1:1 and the
//  assertions target the transport contract (load/read/loop/stop, volume
//  clamping, end-of-file signalling) rather than the resampling maths.
// ═══════════════════════════════════════════════════════════════════

public sealed class BackgroundMusicPlayerTests : IDisposable
{
    private readonly string _tempDir;

    public BackgroundMusicPlayerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_Bgm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    /// <summary>Write a mono IEEE-float WAV at 48 kHz; returns its full path.</summary>
    private string WriteTone(string name, int samples, float amplitude = 0.3f, float hz = 1000f)
    {
        var path = Path.Combine(_tempDir, name);
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        using var w = new WaveFileWriter(path, fmt);
        for (int i = 0; i < samples; i++)
            w.WriteSample(amplitude * MathF.Sin(2f * MathF.PI * hz * i / 48000f));
        w.Flush();
        return path;
    }

    [Fact(DisplayName = "BGM-001: Load exposes readiness, track name and duration; does not auto-play")]
    public void BGM001_LoadReportsReady()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tone.wav", 48000));   // 1 s
        Assert.True(p.IsReady);
        Assert.False(p.IsPlaying);
        Assert.Equal("tone", p.TrackName);
        Assert.True(Math.Abs(p.DurationSec - 1.0) < 0.05, $"duration was {p.DurationSec}");
    }

    [Fact(DisplayName = "BGM-002: ReadSamples yields nothing until Play")]
    public void BGM002_SilenceUntilPlay()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tone.wav", 48000));
        var dest = new float[480];
        Assert.Equal(0, p.ReadSamples(dest));
        p.Play();
        Assert.True(p.IsPlaying);
        Assert.Equal(480, p.ReadSamples(dest));
    }

    [Fact(DisplayName = "BGM-003: played samples stay within [-1,1] for a normalised source")]
    public void BGM003_SampleBounds()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tone.wav", 48000, amplitude: 0.3f));
        p.Play();
        var dest = new float[4800];
        int n = p.ReadSamples(dest);
        Assert.True(n > 0);
        for (int i = 0; i < n; i++) Assert.InRange(dest[i], -1.0001f, 1.0001f);
    }

    [Fact(DisplayName = "BGM-004: Volume clamps to [0,2]")]
    public void BGM004_VolumeClamp()
    {
        using var p = new BackgroundMusicPlayer();
        p.Volume = 5f;   Assert.Equal(2f, p.Volume);
        p.Volume = -3f;  Assert.Equal(0f, p.Volume);
        p.Volume = 0.8f; Assert.Equal(0.8f, p.Volume);
    }

    [Fact(DisplayName = "BGM-005: reaching the end without loop stops and raises PlaybackEnded")]
    public void BGM005_EndRaisesEvent()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tiny.wav", 480));     // 10 ms
        bool ended = false;
        p.PlaybackEnded += () => ended = true;
        p.IsLoop = false;
        p.Play();
        var dest = new float[2000];
        int n = p.ReadSamples(dest);
        Assert.True(n < 2000, $"expected short read at end, got {n}");
        Assert.True(ended, "PlaybackEnded not raised at end of file");
        Assert.False(p.IsPlaying);
    }

    [Fact(DisplayName = "BGM-006: loop wraps and keeps filling the requested frame count")]
    public void BGM006_LoopKeepsPlaying()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tiny.wav", 480));     // 10 ms
        bool ended = false;
        p.PlaybackEnded += () => ended = true;
        p.IsLoop = true;
        p.Play();
        var dest = new float[2000];
        int n = p.ReadSamples(dest);
        Assert.Equal(2000, n);
        Assert.False(ended);
        Assert.True(p.IsPlaying);
    }

    [Fact(DisplayName = "BGM-007: Stop clears the play flag and rewinds the position")]
    public void BGM007_StopRewinds()
    {
        using var p = new BackgroundMusicPlayer();
        p.Load(WriteTone("tone.wav", 48000));
        p.Play();
        p.ReadSamples(new float[4800]);
        p.Stop();
        Assert.False(p.IsPlaying);
        Assert.Equal(0.0, p.PositionSec, 3);
    }
}
