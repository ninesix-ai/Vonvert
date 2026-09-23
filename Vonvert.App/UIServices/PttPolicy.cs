// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <summary>
/// Pure, UI-free decision logic for Push-to-Talk. The window layer keeps the
/// current <see cref="State"/>, feeds each hook transition through
/// <see cref="OnChange"/>, and applies the resulting mute flag (gain) plus any
/// power side-effect. Isolating the state machine here makes both PTT modes
/// unit-testable without a real keyboard hook or audio device.
/// </summary>
public static class PttPolicy
{
    /// <param name="Active">A PTT key transition is currently latched (held down).</param>
    /// <param name="Muted">Effective mute flag the caller should apply to gain.</param>
    /// <param name="WasMuted">Mute flag to restore when the key is released.</param>
    public readonly record struct State(bool Active, bool Muted, bool WasMuted);

    /// <summary>Initial idle state given the current mute flag.</summary>
    public static State Idle(bool muted) => new(Active: false, Muted: muted, WasMuted: muted);

    /// <summary>
    /// Advance the PTT state machine one key transition.
    /// </summary>
    /// <param name="cur">Current state.</param>
    /// <param name="pressed">true on key-down, false on key-up.</param>
    /// <param name="holdToMute">false = Hold-to-Talk (mute by default, talk while held);
    /// true = Hold-to-Mute (talk by default, mute while held).</param>
    public static State OnChange(State cur, bool pressed, bool holdToMute)
    {
        if (pressed && !cur.Active)
        {
            // Latch on key-down. Remember the pre-press mute flag so release can restore it.
            bool muted = holdToMute;                 // Hold-to-Mute mutes while held; Hold-to-Talk unmutes.
            return new State(Active: true, Muted: muted, WasMuted: cur.Muted);
        }

        if (!pressed && cur.Active)
        {
            // Release restores whatever the mute state was before the press.
            return new State(Active: false, Muted: cur.WasMuted, WasMuted: cur.WasMuted);
        }

        return cur;   // duplicate press / release while idle → no change
    }
}
