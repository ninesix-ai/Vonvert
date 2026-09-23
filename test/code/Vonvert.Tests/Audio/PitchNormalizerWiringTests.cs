// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

public class PitchNormalizerWiringTests
{
    [Fact]
    public void NullAudioProcessor_ExposesNormalizer_Control()
    {
        var p = new NullAudioProcessor();
        Assert.NotNull(p.PitchNormalizer);
        Assert.False(p.PitchNormalizer.Enabled);          // default off
        p.PitchNormalizer.TargetF0Hz = 220f;
        p.PitchNormalizer.FallbackSemitones = 4f;
        p.PitchNormalizer.Enabled = true;                 // must not throw off the audio thread
        p.PitchNormalizer.Reset();
        Assert.True(p.PitchNormalizer.Enabled);
        p.Dispose();
    }
}
