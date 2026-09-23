// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression tests for DeviceSelection.ResolveInput — the Settings UI's
// microphone-dropdown fallback rule. The bug: after clicking "Refresh Devices",
// when no saved selection matched, the UI fell back to inputs[0]. A virtual
// audio cable's recording endpoint ("CABLE Output" / "VoiceMeeter …") frequently
// enumerates first, so the Input Microphone silently switched from the user's
// real default microphone to the virtual cable.

using Vonvert.Engine.AudioEngine;
using Xunit;

namespace Vonvert.Tests.Audio;

public class DeviceSelectionTests
{
    private static AudioDevice Dev(string id, string name)
        => new(id, name, 2, 48000, 16);

    // The core regression: virtual cable enumerates first, no saved selection,
    // system default mic is present elsewhere in the list.
    [Fact]
    public void RI_001_NoSavedSelection_PrefersSystemDefault_NotVirtualCableFirstInList()
    {
        var inputs = new List<AudioDevice>
        {
            Dev("cable-out", "CABLE Output (VB-Audio Virtual Cable)"),
            Dev("vm-out",    "VoiceMeeter Out B1 (VB-Audio VoiceMeeter)"),
            Dev("real-mic",  "Headset Microphone"),
        };

        var sel = DeviceSelection.ResolveInput(inputs, savedId: "", defaultInputId: "real-mic");

        Assert.NotNull(sel);
        Assert.Equal("real-mic", sel!.Id);
    }

    // A saved selection must always win, even if it is not the default.
    [Fact]
    public void RI_002_SavedSelectionPresent_KeepsSavedEvenWhenNotDefault()
    {
        var inputs = new List<AudioDevice>
        {
            Dev("cable-out", "CABLE Output (VB-Audio Virtual Cable)"),
            Dev("real-mic",  "Headset Microphone"),
            Dev("usb-mic",   "USB Conference Mic"),
        };

        var sel = DeviceSelection.ResolveInput(inputs, savedId: "usb-mic", defaultInputId: "real-mic");

        Assert.Equal("usb-mic", sel!.Id);
    }

    // Saved id is stale (device removed): must fall through to the system default.
    [Fact]
    public void RI_003_SavedSelectionStale_FallsBackToSystemDefault()
    {
        var inputs = new List<AudioDevice>
        {
            Dev("cable-out", "CABLE Output (VB-Audio Virtual Cable)"),
            Dev("real-mic",  "Headset Microphone"),
        };

        var sel = DeviceSelection.ResolveInput(inputs, savedId: "removed-mic-id", defaultInputId: "real-mic");

        Assert.Equal("real-mic", sel!.Id);
    }

    // No saved id AND no discoverable default: last-resort returns inputs[0].
    [Fact]
    public void RI_004_NoSavedAndNoDefault_FallsBackToFirstEntry()
    {
        var inputs = new List<AudioDevice>
        {
            Dev("cable-out", "CABLE Output (VB-Audio Virtual Cable)"),
            Dev("real-mic",  "Headset Microphone"),
        };

        var sel = DeviceSelection.ResolveInput(inputs, savedId: null, defaultInputId: null);

        Assert.Equal("cable-out", sel!.Id);
    }

    // Empty input list must not throw and returns null.
    [Fact]
    public void RI_005_EmptyList_ReturnsNull()
    {
        var inputs = new List<AudioDevice>();

        var sel = DeviceSelection.ResolveInput(inputs, savedId: "", defaultInputId: "whatever");

        Assert.Null(sel);
    }

    // Default id points to a device not in the list (disconnected): falls back
    // to inputs[0] rather than returning null.
    [Fact]
    public void RI_006DefaultIdNotInList_FallsBackToFirstEntry()
    {
        var inputs = new List<AudioDevice>
        {
            Dev("cable-out", "CABLE Output (VB-Audio Virtual Cable)"),
            Dev("real-mic",  "Headset Microphone"),
        };

        var sel = DeviceSelection.ResolveInput(inputs, savedId: "", defaultInputId: "offline-default");

        Assert.Equal("cable-out", sel!.Id);
    }
}
