// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Pure device-selection fallback rules used by the Settings UI when it
/// (re)populates the microphone dropdown. Kept free of WPF/NAudio so it is
/// unit-testable and so the UI's default-resolution rule stays consistent with
/// the engine (see <c>VoiceEngine.InitGraph</c>).
/// </summary>
public static class DeviceSelection
{
    /// <summary>
    /// Decide which input (capture) device to select for the microphone list.
    ///
    /// Resolution order:
    ///  1. the previously saved device, if it is still present;
    ///  2. otherwise the SYSTEM DEFAULT input device, if present;
    ///  3. otherwise the first entry in the list;
    ///  4. otherwise null (no inputs available).
    ///
    /// Step 2 is the important part: when there is no saved match the UI must
    /// fall back to the system default microphone and NEVER blindly to the
    /// first enumerated device. A virtual cable's recording endpoint
    /// ("CABLE Output", "VoiceMeeter …") frequently enumerates before the real
    /// microphone and would otherwise silently replace the user's mic.
    /// </summary>
    /// <param name="inputs">Active capture devices, in enumeration order.</param>
    /// <param name="savedId">The last user-selected input id (may be null/empty).</param>
    /// <param name="defaultInputId">The Windows default capture device id (may be null/empty).</param>
    public static AudioDevice? ResolveInput(
        IReadOnlyList<AudioDevice> inputs,
        string? savedId,
        string? defaultInputId)
    {
        if (inputs is null || inputs.Count == 0) return null;

        if (!string.IsNullOrEmpty(savedId))
        {
            var saved = inputs.FirstOrDefault(d => d.Id == savedId);
            if (saved is not null) return saved;
        }

        if (!string.IsNullOrEmpty(defaultInputId))
        {
            var def = inputs.FirstOrDefault(d => d.Id == defaultInputId);
            if (def is not null) return def;
        }

        return inputs[0];
    }
}
