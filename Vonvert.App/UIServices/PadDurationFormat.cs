// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;

namespace Vonvert.App.UIServices;

/// <summary>Formats soundboard pad durations as m:ss. Sub-second sounds display 0:01
/// (never 0:00). Single source of truth for the pad duration label rule.</summary>
public static class PadDurationFormat
{
    public static string Format(double seconds)
    {
        int whole = (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
        if (whole < 1) whole = 1;
        return $"{whole / 60}:{whole % 60:D2}";
    }
}
