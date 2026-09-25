// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.Audio;

using Vonvert.Engine.AudioEngine;
using Xunit;

public sealed class SoundboardAuditionPlayerTests
{
    [Theory(DisplayName = "AUD-IDLE-001: stop only once silence ticks reach threshold")]
    [InlineData(0, 4, false)]
    [InlineData(3, 4, false)]
    [InlineData(4, 4, true)]
    [InlineData(9, 4, true)]
    public void IdleDecider_Threshold(int ticks, int stopAfter, bool expected)
        => Assert.Equal(expected, IdleDecider.ShouldStop(ticks, stopAfter));
}
