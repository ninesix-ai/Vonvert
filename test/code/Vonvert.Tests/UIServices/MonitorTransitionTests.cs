// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using Xunit;
using Vonvert.App.UIServices;

namespace Vonvert.Tests.UIServices;

// TR-001 ~ TR-005: which panels a layout change should ease in.
//
// Switching layout was a hard cut: panels vanished and reappeared at full brightness in
// one frame, which reads as a glitch on a captured stream. Fading is the cheap fix, but
// only the panels that actually *became* visible may fade - fading one that was already
// showing makes the picture blink. That decision belongs in a pure function, because the
// alternative is an if-chain in the render path nobody can test.
public sealed class MonitorTransitionTests
{
    [Fact(DisplayName = "TR-001: filling a panel fades nothing in - it was already on screen")]
    public void TR001_MaximizeFadesNothing()
        => Assert.Empty(MonitorTransition.PanesToFade(null, MonitorPane.Waterfall));

    [Fact(DisplayName = "TR-002: leaving a filled panel eases in exactly the two panels that were hidden")]
    public void TR002_RestoringEasesInHiddenPanels()
    {
        var faded = MonitorTransition.PanesToFade(MonitorPane.Waterfall, null);
        Assert.Equal(2, faded.Count);
        Assert.Contains(MonitorPane.Waveform, faded);
        Assert.Contains(MonitorPane.Loudness, faded);
        Assert.DoesNotContain(MonitorPane.Waterfall, faded);   // it never left; a fade would blink
    }

    [Fact(DisplayName = "TR-003: swapping which panel is filled eases in only the new one")]
    public void TR003_SwapEasesInIncomingOnly()
        => Assert.Equal(new[] { MonitorPane.Loudness },
            MonitorTransition.PanesToFade(MonitorPane.Waterfall, MonitorPane.Loudness));

    [Fact(DisplayName = "TR-004: staying in the grid fades nothing")]
    public void TR004_GridToGrid()
        => Assert.Empty(MonitorTransition.PanesToFade(null, null));

    [Fact(DisplayName = "TR-005: the duration is short, and reduce-motion turns the animation off completely")]
    public void TR005_DurationAndReduceMotion()
    {
        Assert.Equal(0, MonitorTransition.DurationMs(reduceMotion: true));
        int normal = MonitorTransition.DurationMs(reduceMotion: false);
        Assert.True(normal > 0 && normal <= 250,
            $"transition is {normal} ms; it has to be visible without making a live switch feel laggy");
    }
}
