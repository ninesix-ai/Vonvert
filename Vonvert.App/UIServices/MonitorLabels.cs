// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <summary>Which vocabulary the monitor's panel titles speak.</summary>
public enum MonitorLabelMode
{
    /// <summary>Plain words ("Your raw voice"), the default for non-audio people.</summary>
    Plain,
    /// <summary>Industry terms ("Wet"), for engineers and for matching the manuals.</summary>
    Technical,
}

/// <summary>A label slot of the monitor, independent of how it is worded.</summary>
public enum MonitorLabelSlot { Spectrogram, Waveform, Loudness, Dry, Wet }

/// <summary>
/// Maps a monitor label slot to the translation key to show, for the active
/// vocabulary mode. Pure and WPF-free so the pairing (and the rule that a technical
/// name is never printed twice) is unit-testable, and so a mistyped key can be caught
/// against the shipped language files instead of silently rendering "FsDryPlain".
/// </summary>
public sealed class MonitorLabels
{
    // Plain is the default on purpose: the window used to open with Spectrogram /
    // LUFS / Dry / Wet on screen, which is the wall that keeps a new streamer out.
    public MonitorLabelMode Mode { get; private set; } = MonitorLabelMode.Plain;

    public void Toggle()
        => Mode = Mode == MonitorLabelMode.Plain
                ? MonitorLabelMode.Technical
                : MonitorLabelMode.Plain;

    /// <summary>Translation key for the main label of a slot.</summary>
    public string KeyFor(MonitorLabelSlot slot)
        => Mode == MonitorLabelMode.Plain ? PlainKey(slot) : TechnicalKey(slot);

    /// <summary>
    /// Translation key for the small secondary label, or null when there must not be
    /// one. Plain mode keeps the industry term underneath, so switching never hides
    /// vocabulary; technical mode would otherwise print the same word twice.
    /// </summary>
    public string? SubKeyFor(MonitorLabelSlot slot)
        => Mode == MonitorLabelMode.Plain ? TechnicalKey(slot) : null;

    private static string PlainKey(MonitorLabelSlot slot) => slot switch
    {
        MonitorLabelSlot.Spectrogram => "FsSpectrogramPlain",
        MonitorLabelSlot.Waveform    => "FsWaveformPlain",
        MonitorLabelSlot.Loudness    => "FsLoudnessPlain",
        MonitorLabelSlot.Dry         => "FsDryPlain",
        _                            => "FsWetPlain",
    };

    private static string TechnicalKey(MonitorLabelSlot slot) => slot switch
    {
        MonitorLabelSlot.Spectrogram => "FsSpectrogram",
        MonitorLabelSlot.Waveform    => "FsWaveform",
        MonitorLabelSlot.Loudness    => "FsLoudness",
        MonitorLabelSlot.Dry         => "FsDry",
        _                            => "FsWet",
    };
}
