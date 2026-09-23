// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Auto-split from LocalizationManager.cs — do not edit manually.

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Settings tab ──
    public string Settings       => G();
    public string AudioDevices   => G();
    public string InputMic       => G();
    public string OutputVbCable  => G();
    public string VbCableDetected => G();
    public string InstallVbCable => G();
    public string InstallVbCableLink => G();
    public string DeviceHint     => G();
    public string NoDevices      => G();

    // ── Voice Processing card ──
    public string RefreshDevices   => G();

}
