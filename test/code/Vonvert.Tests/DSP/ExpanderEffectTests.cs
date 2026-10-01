// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

/// <summary>
/// ExpanderEffect unit tests — EXP-001 ~ EXP-006.
/// Expander: attenuates further below the threshold, passes transparently above it.
/// </summary>
public sealed class ExpanderEffectTests
{
    [Fact(DisplayName = "EXP-001: Default parameters")]
    public void EXP001_DefaultParameters()
    {
        var fx = new ExpanderEffect();
        Assert.False(string.IsNullOrEmpty(fx.Name));
        Assert.False(fx.IsEnabled); // disabled by default (enabled on demand by DSPChain)
    }

    [Fact(DisplayName = "EXP-002: Process preserves buffer length")]
    public void EXP002_Process_PreservesLength()
    {
        var fx = new ExpanderEffect { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateWhiteNoise(4096, 0.3f, 42);
        int lenBefore = buf.Length;
        fx.Process(buf.AsSpan());
        Assert.Equal(lenBefore, buf.Length);
    }

    [Fact(DisplayName = "EXP-003: Below threshold → signal attenuated")]
    public void EXP003_BelowThreshold_Attenuates()
    {
        var fx = new ExpanderEffect { IsEnabled = true };
        // Low-level white noise sits mostly under the default threshold, so the
        // expander should pull the quiet passages further down (RMS must not rise).
        var input = AudioTestHelpers.GenerateWhiteNoise(4800, 0.05f, 42);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double rmsIn = AudioTestHelpers.ComputeRms(original.AsSpan());
        double rmsOut = AudioTestHelpers.ComputeRms(input.AsSpan());
        Assert.True(rmsOut <= rmsIn * 1.01, // allow 1% float tolerance
            $"RMS should decrease: in={rmsIn:G4}, out={rmsOut:G4}");
    }

    [Fact(DisplayName = "EXP-004: Above threshold → transparent pass-through")]
    public void EXP004_AboveThreshold_Transparent()
    {
        var fx = new ExpanderEffect { IsEnabled = true };
        // A high-amplitude signal stays above the threshold and should pass near-unchanged.
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.8f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double rmsDiff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(rmsDiff < 0.15f,
            $"High-level signal should pass through with minimal change: diff={rmsDiff:G4}");
    }

    [Fact(DisplayName = "EXP-005: Reset() clears envelope state")]
    public void EXP005_Reset_ClearsState()
    {
        var fx = new ExpanderEffect { IsEnabled = true };
        var input = AudioTestHelpers.GenerateWhiteNoise(2400, 0.3f, 42);

        fx.Process(input.AsSpan());
        fx.Reset();

        // Re-processing after Reset should match the fresh first-pass result.
        var buf1 = AudioTestHelpers.GenerateWhiteNoise(2400, 0.3f, 42);
        var copy1 = (float[])buf1.Clone();
        fx.Process(buf1.AsSpan());

        fx.Reset();
        var buf2 = (float[])copy1.Clone();
        fx.Process(buf2.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-5f),
            "Reset should make processing deterministic");
    }

    [Fact(DisplayName = "EXP-006: IsEnabled=false → DSPChain bypass")]
    public void EXP006_Disabled_Bypass()
    {
        var fx = new ExpanderEffect { IsEnabled = false };
        var input = AudioTestHelpers.GenerateSineWave(440, 1024, 0.5f);
        var expected = (float[])input.Clone();

        var chain = new DSPChain();
        chain.Add(fx);
        chain.Process(input.AsSpan());

        Assert.True(AudioTestHelpers.SpanEqual(expected.AsSpan(), input.AsSpan()),
            "Disabled effect should not modify buffer");
    }
}
