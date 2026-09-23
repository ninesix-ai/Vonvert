// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guards for the Push-to-Talk state machine. PTT has two modes with
// opposite key-down semantics, and both must restore the *pre-press* mute state
// on release — including when the user was already muted (or unmuted) before the
// press. These exercise the pure policy directly, no hook or audio device.

namespace Vonvert.Tests.UIServices;

using Vonvert.App.UIServices;
using Xunit;

public class PttPolicyTests
{
    private static PttPolicy.State Hold(PttPolicy.State s, bool pressed, bool holdToMute)
        => PttPolicy.OnChange(s, pressed, holdToMute);

    [Fact(DisplayName = "PTT-TALK-01: Hold-to-Talk unmutes on press, restores on release")]
    public void HoldToTalk_UnmutesThenRestores()
    {
        var start = PttPolicy.Idle(muted: true);   // user was muted before pressing

        var held = Hold(start, pressed: true, holdToMute: false);
        Assert.True(held.Active);
        Assert.False(held.Muted);                 // talking now
        Assert.True(held.WasMuted);               // remembers the prior mute

        var released = Hold(held, pressed: false, holdToMute: false);
        Assert.False(released.Active);
        Assert.True(released.Muted);              // restored to muted
    }

    [Fact(DisplayName = "PTT-MUTE-01: Hold-to-Mute mutes on press, restores on release")]
    public void HoldToMute_MutesThenRestores()
    {
        var start = PttPolicy.Idle(muted: false);

        var held = Hold(start, pressed: true, holdToMute: true);
        Assert.True(held.Active);
        Assert.True(held.Muted);                  // muted while held

        var released = Hold(held, pressed: false, holdToMute: true);
        Assert.False(released.Active);
        Assert.False(released.Muted);             // restored to unmuted
    }

    [Fact(DisplayName = "PTT-IDEM-01: duplicate press while held, and release while idle, are no-ops")]
    public void DuplicatePressAndStrayRelease_AreNoOps()
    {
        var start = PttPolicy.Idle(muted: false);

        var held = Hold(start, pressed: true, holdToMute: true);
        var heldAgain = Hold(held, pressed: true, holdToMute: true);
        Assert.Equal(held, heldAgain);            // second key-down (auto-repeat) ignored

        var idle = PttPolicy.Idle(muted: false);
        var strayRelease = Hold(idle, pressed: false, holdToMute: false);
        Assert.Equal(idle, strayRelease);         // release without a press ignored
    }

    [Fact(DisplayName = "PTT-NEST-01: WasMuted captures the immediate pre-press state, not an older one")]
    public void WasMutedTracksImmediatePrePressState()
    {
        // Start unmuted, hold-to-mute press → WasMuted=false.
        var held = Hold(PttPolicy.Idle(muted: false), pressed: true, holdToMute: true);
        Assert.False(held.WasMuted);

        // Release restores unmuted; press again while that release is settled.
        var released = Hold(held, pressed: false, holdToMute: true);
        Assert.False(released.Muted);
        var heldAgain = Hold(released, pressed: true, holdToMute: true);
        Assert.False(heldAgain.WasMuted);
    }
}
