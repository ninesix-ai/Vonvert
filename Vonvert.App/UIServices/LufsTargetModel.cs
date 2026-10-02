// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>Which loudness target the meter draws its reference line for.</summary>
public enum LufsTargetPreset
{
    /// <summary>EBU R128 broadcast (-23 LUFS). The shipped default.</summary>
    Broadcast,
    /// <summary>Typical streaming platforms (about -14 LUFS).</summary>
    Streaming,
    /// <summary>Typical podcast loudness (about -16 LUFS).</summary>
    Podcast,
    /// <summary>No reference line at all.</summary>
    Hidden,
}

/// <summary>
/// The reference line on the loudness meter.
///
/// The line used to be welded to -23, which is the broadcast number: a podcaster aiming
/// at -16 and a streamer at -14 were told to sit 7 to 9 LUFS quieter than their platform,
/// and the meter looked like it said they were too quiet all evening. Default stays at
/// broadcast so nothing changes for anyone who never touches it.
/// </summary>
public sealed class LufsTargetModel
{
    public const double BroadcastLUFS = -23;
    public const double StreamingLUFS = -14;
    public const double PodcastLUFS = -16;

    public LufsTargetPreset Preset { get; private set; } = LufsTargetPreset.Broadcast;

    /// <summary>Loudness of the current target, or null when the line is hidden.</summary>
    public double? LUFS => Preset switch
    {
        LufsTargetPreset.Broadcast => BroadcastLUFS,
        LufsTargetPreset.Streaming => StreamingLUFS,
        LufsTargetPreset.Podcast => PodcastLUFS,
        _ => null,
    };

    /// <summary>Translation key naming the current target, shown on the button and tooltip.</summary>
    public string LabelKey => Preset switch
    {
        LufsTargetPreset.Broadcast => "FsTargetBroadcast",
        LufsTargetPreset.Streaming => "FsTargetStreaming",
        LufsTargetPreset.Podcast => "FsTargetPodcast",
        _ => "FsTargetNone",
    };

    public void Cycle() => Preset = Preset switch
    {
        LufsTargetPreset.Broadcast => LufsTargetPreset.Streaming,
        LufsTargetPreset.Streaming => LufsTargetPreset.Podcast,
        LufsTargetPreset.Podcast => LufsTargetPreset.Hidden,
        _ => LufsTargetPreset.Broadcast,
    };

    public void Set(LufsTargetPreset preset) => Preset = preset;
}

/// <summary>How close the true-peak reading is to distortion.</summary>
public enum PeakAlert { Ok, Warning, Clip }

/// <summary>
/// Turns the true-peak number into a traffic light. A number alone in dBTP means nothing
/// to a streamer; "you are about to distort" does, and it is the one fault that cannot be
/// fixed after recording.
/// </summary>
public static class LufsPeakAlert
{
    /// <summary>Common delivery ceiling: -1 dBTP keeps headroom for lossy encoding.</summary>
    public const double WarningDb = -1;

    /// <summary>Above this the signal is clipping and will audibly crackle.</summary>
    public const double ClipDb = 0;

    public static PeakAlert LevelFor(double truePeakDb)
    {
        if (double.IsNaN(truePeakDb) || double.IsNegativeInfinity(truePeakDb)) return PeakAlert.Ok;
        if (truePeakDb >= ClipDb) return PeakAlert.Clip;
        if (truePeakDb > WarningDb) return PeakAlert.Warning;
        return PeakAlert.Ok;
    }

    /// <summary>Translation key for the alert, or null when there is nothing to warn about.</summary>
    public static string? KeyFor(PeakAlert alert) => alert switch
    {
        PeakAlert.Warning => "FsTpWarning",
        PeakAlert.Clip => "FsTpClip",
        _ => null,
    };
}
