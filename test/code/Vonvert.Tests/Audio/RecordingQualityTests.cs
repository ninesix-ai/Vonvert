// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

/// <summary>
/// Guards the single-source-of-truth sample rate shared by the whole audio
/// graph and every file recorder, so the WAV header rate always matches the
/// real DSP chain rate.
/// </summary>
public sealed class RecordingQualityTests
{
    [Fact(DisplayName = "RQ-001: EngineSettings default sample rate matches the unified engine rate")]
    public void EngineSettings_DefaultSampleRate_MatchesUnifiedRate()
    {
        // The engine and every file recorder share a single 48 kHz DSP rate
        // (AudioConstants.EngineRate); there is no divergent 44100 default.
        var config = new EngineSettings();
        Assert.Equal(AudioConstants.EngineRate, config.SampleRate);
        Assert.Equal(48000, config.SampleRate);
    }
}
