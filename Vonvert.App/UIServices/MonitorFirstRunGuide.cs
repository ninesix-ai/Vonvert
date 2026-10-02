// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using Vonvert.Engine;

namespace Vonvert.App.UIServices;

/// <summary>
/// Decides whether the monitor's first-run walkthrough appears, and where its one-time
/// marker lives. Separated from the view so the marker identity is testable: reusing the
/// main tour's marker file would mark the first-run walkthrough as done on every new
/// install the moment somebody finished the monitor guide.
/// </summary>
public static class MonitorFirstRunGuide
{
    /// <summary>Must stay different from "tour_done" and "onboarding_done".</summary>
    public const string MarkerFileName = "fs_monitor_tour_done";

    public static string MarkerPath => Path.Combine(AppPaths.Root, MarkerFileName);

    /// <summary>Shown once per install; nothing to configure and nothing to gate.</summary>
    public static bool ShouldShow(bool markerFileExists) => !markerFileExists;

    /// <summary>Translation key of each walkthrough stop, in spotlight order. The stops
    /// follow the reading order of the window: big picture, then level, then source.</summary>
    public static string[] StepKeys { get; } =
    {
        "FsTourWaterfall", "FsTourLoudness", "FsTourWaveform", "FsTourChrome",
    };
}
