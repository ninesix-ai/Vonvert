// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows.Input;

namespace Vonvert.App.UIServices;

/// <summary>What a key press inside the monitor means.</summary>
public enum MonitorKeyAction
{
    Close, ToggleHelp, ToggleTopmost, CycleSize, ResetView, ShowDry, ShowWet, ToggleTarget,

    /// <summary>Keyboard equivalent of double-clicking a pane: steps through
    /// none, voice detail, volume, voice shape.</summary>
    MaximizeNext,
}

/// <summary>
/// Keyboard map for the fullscreen monitor.
///
/// Everything in this window used to be a mouse gesture, so anyone without a mouse - or
/// a streamer whose hands are on a controller - could open the window and then not close
/// it, change the size, or switch the waveform source. Plain letters are safe here
/// because the settings screen only accepts a modifier combination or an F-key for a
/// global hotkey, so none of these can collide with a user's own binding.
/// </summary>
public static class MonitorKeys
{
    /// <summary>Mapped action for a key press, or null when it belongs to somebody else.</summary>
    public static MonitorKeyAction? Map(Key key, ModifierKeys modifiers)
    {
        // Any modifier (Ctrl, Alt, Shift) means the user aimed it at something else; the
        // window must swallow nothing that a browser-style shortcut might need.
        if (modifiers != ModifierKeys.None) return null;

        return key switch
        {
            Key.Escape => MonitorKeyAction.Close,
            Key.H or Key.F1 => MonitorKeyAction.ToggleHelp,
            Key.T => MonitorKeyAction.ToggleTopmost,
            Key.S => MonitorKeyAction.CycleSize,
            Key.R => MonitorKeyAction.ResetView,
            Key.D => MonitorKeyAction.ShowDry,
            Key.W => MonitorKeyAction.ShowWet,
            Key.G => MonitorKeyAction.ToggleTarget,
            Key.Enter => MonitorKeyAction.MaximizeNext,
            _ => null,
        };
    }

    /// <summary>
    /// One line for the help card and the guide. The letters are the information, so they
    /// stay as-is in every language and nothing here is translatable prose.
    /// </summary>
    public static string ShortcutSummary =>
        "H help · T topmost · S size · R reset · D raw · W changed · G target · Esc close";
}
