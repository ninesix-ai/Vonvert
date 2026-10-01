// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>
/// Maps LUFS/dB values onto the vertical loudness meter. Range is a fixed
/// [-60, 0] window: 0 dB sits at the top (ratio 0), -60 at the bottom (ratio 1).
/// Out-of-range values clamp; the mapping is linear and monotonic.
/// </summary>
public static class LufsScale
{
    public const float FloorDb = -60f;
    public const float CeilingDb = 0f;
    public const float TargetLufs = -23f;   // EBU R128 broadcast target

    public static double ToRatio(float db)
    {
        float c = Math.Clamp(db, FloorDb, CeilingDb);
        return (CeilingDb - c) / (CeilingDb - FloorDb);
    }

    public static double MapToPixel(float db, double height) => ToRatio(db) * height;
}
