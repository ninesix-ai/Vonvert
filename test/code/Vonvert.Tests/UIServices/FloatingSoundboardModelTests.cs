// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UIServices;

using System;
using Vonvert.App.UIServices;
using Xunit;

// The floating mini-player shows only pinned pads, in pin order, dropping ids whose sound
// no longer exists. That selection rule is UI-free so it can be pinned without STA/audio.
public sealed class FloatingSoundboardModelTests
{
    [Fact(DisplayName = "FSB-001: OrderPinned returns pinned ids that still exist, in pin order, de-duped")]
    public void OrderPinned_FiltersExistingInPinOrder()
    {
        var pinned = new[] { "airhorn", "kick", "ghost", "airhorn" };
        var existing = new[] { "kick", "airhorn", "snare" };
        var result = FloatingSoundboardModel.OrderPinned(pinned, existing);
        Assert.Equal(new[] { "airhorn", "kick" }, result);
    }

    [Fact(DisplayName = "FSB-002: empty pinned set yields empty (drives empty-state)")]
    public void OrderPinned_EmptyWhenNothingPinned()
    {
        Assert.Empty(FloatingSoundboardModel.OrderPinned(Array.Empty<string>(), new[] { "kick" }));
    }
}
