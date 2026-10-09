// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Background music playback contract.
/// Implemented by <see cref="BackgroundMusicPlayer"/>.
/// </summary>
public interface IBgmPlayer
{
    bool IsPlaying { get; }
    bool IsReady { get; }
    string TrackName { get; }
    double DurationSec { get; }
    double PositionSec { get; }
    bool IsLoop { get; set; }
    float Volume { get; set; }
    double FadeInDurationSec { get; set; }
    double FadeOutDurationSec { get; set; }
    void Load(string path);
    void Play();
    void Pause();
    void Stop();
    void Seek(double positionSec);
}
