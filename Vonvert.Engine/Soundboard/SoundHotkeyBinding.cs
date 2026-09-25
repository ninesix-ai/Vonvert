// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.Soundboard;

/// <summary>
/// Represents a hotkey binding for a soundboard sound.
/// Stores the virtual key code and modifier flags.
/// </summary>
/// <param name="VirtualKey">Windows virtual key code (VK).</param>
/// <param name="Ctrl">Whether Ctrl modifier is required.</param>
/// <param name="Alt">Whether Alt modifier is required.</param>
/// <param name="Shift">Whether Shift modifier is required.</param>
public record SoundHotkeyBinding(
    int VirtualKey,
    bool Ctrl = false,
    bool Alt = false,
    bool Shift = false
);
