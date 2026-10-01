// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <content>
/// Fullscreen professional monitor labels (header entry tooltip, pane headers,
/// dry/wet taps, loudness meter).
/// </content>
public partial class LocalizationManager
{
    // ── Fullscreen monitor ──
    public string FullscreenViz      => G();
    public string EscToClose         => G();
    public string FsSpectrogram      => G();
    public string FsWaveform         => G();
    public string FsDry              => G();
    public string FsWet              => G();
    public string FsLoudness         => G();
    public string FsTargetLufs       => G();
    public string FsIntegrated       => G();
    public string FsResetIntegrated  => G();
}
