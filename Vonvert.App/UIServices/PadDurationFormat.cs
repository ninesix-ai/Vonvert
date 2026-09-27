// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.App.UIServices;

/// <summary>Formats soundboard pad durations in tiers that match what the ear hears:
/// sub-second as whole milliseconds (50ms), 1-10 s with one decimal (1.2s), 10 s and
/// up as whole seconds (30s). Single source of truth for the pad duration label rule.</summary>
public static class PadDurationFormat
{
    public static string Format(double seconds)
    {
        if (seconds < 1.0)
        {
            int ms = (int)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero);
            if (ms < 1) ms = 1;
            return ms + "ms";
        }
        if (seconds < 10.0)
        {
            double oneDec = Math.Round(seconds, 1, MidpointRounding.AwayFromZero);
            if (oneDec >= 10.0) return ((int)Math.Round(oneDec, MidpointRounding.AwayFromZero)) + "s";
            return oneDec == Math.Floor(oneDec)
                ? ((int)oneDec).ToString() + "s"
                : oneDec.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }
        return ((int)Math.Round(seconds, MidpointRounding.AwayFromZero)) + "s";
    }
}
