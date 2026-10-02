// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

/// <summary>What the monitor should tell the user about the signal it is showing.</summary>
public enum GuidanceKind
{
    /// <summary>Everything is live and speaks for itself; show nothing.</summary>
    None,
    /// <summary>No engine to read from yet (voice changing is off / not started).</summary>
    EngineNotReady,
    /// <summary>Engine is analysing and nothing has come in for a while.</summary>
    NoInput,
    /// <summary>Informational: A/B is on DRY, so the meters describe the raw voice rather
    /// than what the audience hears. It used to be a fault - the analyzers were skipped
    /// and the picture froze - until the worker started feeding them in every mode.</summary>
    RawVoice,
}

/// <summary>
/// Decides the one banner the fullscreen monitor shows when its picture is not telling
/// the truth. Pure and WPF-free so the priority order is unit-tested instead of
/// discovered by a confused streamer: before this existed the window simply froze or
/// stayed black, which reads as a crash.
/// </summary>
public sealed class MonitorGuidanceModel
{
    /// <summary>How long a dead input must last before it is called "no input". Shorter
    /// than this and the banner would blink between sentences.</summary>
    public const double SilenceSeconds = 3.0;

    /// <summary>Absolute sample peak below which the signal counts as silence.</summary>
    public const double SilentPeakLevel = 0.001;

    public GuidanceKind Evaluate(bool engineReady, bool showingRawVoice, double peakLevel, double secondsSincePeak)
    {
        // Order matters: a fault is worth more attention than an explanation. "Showing the
        // raw voice" is only context, so a dead microphone still gets the louder message.
        if (!engineReady) return GuidanceKind.EngineNotReady;

        bool quiet = peakLevel <= SilentPeakLevel && secondsSincePeak >= SilenceSeconds;
        if (quiet) return GuidanceKind.NoInput;
        if (showingRawVoice) return GuidanceKind.RawVoice;
        return GuidanceKind.None;
    }

    /// <summary>Translation key of the sentence for a guidance state. Kept next to the
    /// enum so a new state cannot forget its copy.</summary>
    public static string KeyFor(GuidanceKind kind) => kind switch
    {
        GuidanceKind.EngineNotReady => "FsStateNotRunning",
        GuidanceKind.NoInput        => "FsStateNoInput",
        GuidanceKind.RawVoice       => "FsStateRawVoice",
        _                           => "",
    };
}
