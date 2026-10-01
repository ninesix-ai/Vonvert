// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Xunit;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Tests.Helpers;

namespace Vonvert.Tests.DSP;

/// <summary>
/// VxDuckingCompressor unit tests — DC-001 ~ DC-006.
/// Ducking compressor: envelope follower + gain reduction triggered by input amplitude.
/// </summary>
public sealed class VxDuckingCompressorTests
{
    [Fact(DisplayName = "DC-001: Default parameters")]
    public void DC001_DefaultParameters()
    {
        var fx = new VxDuckingCompressor();
        Assert.Equal("DuckingCompressor", fx.Name);
        Assert.False(fx.IsEnabled); // disabled by default
        Assert.Equal(-20f, fx.ThresholdDb, 0.01f);
        Assert.Equal(4f, fx.Ratio, 0.01f);
        Assert.Equal(2f, fx.AttackMs, 0.01f);
        Assert.Equal(200f, fx.ReleaseMs, 0.01f);
        Assert.Equal(-20f, fx.RangeDb, 0.01f);
    }

    [Fact(DisplayName = "DC-002: Process produces finite output")]
    public void DC002_Process_FiniteOutput()
    {
        var fx = new VxDuckingCompressor { IsEnabled = true };
        var buf = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);

        fx.Process(buf.AsSpan());

        Assert.All(buf, v => Assert.True(float.IsFinite(v)));
    }

    [Fact(DisplayName = "DC-003: Loud signal → gain reduction applied")]
    public void DC003_LoudSignal_GainReduction()
    {
        var fx = new VxDuckingCompressor
        {
            IsEnabled = true,
            ThresholdDb = -20f,
            Ratio = 10f,
        };

        // Loud sine should trigger ducking
        var input = AudioTestHelpers.GenerateSineWave(1000, 48000, 0.8f);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        // Compare tail portion (after envelope has settled)
        int tailStart = 24000;
        double rmsIn = AudioTestHelpers.ComputeRms(original.AsSpan(tailStart, 24000));
        double rmsOut = AudioTestHelpers.ComputeRms(input.AsSpan(tailStart, 24000));

        // Ducking should reduce the level
        Assert.True(rmsOut < rmsIn * 0.95,
            $"Ducking should reduce RMS: in={rmsIn:G4}, out={rmsOut:G4}");
    }

    [Fact(DisplayName = "DC-004: Very quiet signal → minimal change")]
    public void DC004_QuietSignal_MinimalChange()
    {
        var fx = new VxDuckingCompressor
        {
            IsEnabled = true,
            ThresholdDb = -40f, // high threshold
            Ratio = 2f,
        };

        // Very quiet noise should be below threshold → pass through
        var input = AudioTestHelpers.GenerateWhiteNoise(4800, 0.001f, 42);
        var original = (float[])input.Clone();

        fx.Process(input.AsSpan());

        double diff = AudioTestHelpers.ComputeRmsDifference(original.AsSpan(), input.AsSpan());
        Assert.True(diff < 0.001f,
            $"Very quiet signal should pass through with minimal change: diff={diff:G4}");
    }

    [Fact(DisplayName = "DC-005: Reset() makes processing deterministic")]
    public void DC005_Reset_Deterministic()
    {
        var fx = new VxDuckingCompressor { IsEnabled = true };
        var input = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);

        fx.Process(input.AsSpan());
        fx.Reset();

        // After reset, processing same signal should produce identical output
        var buf1 = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        fx.Process(buf1.AsSpan());

        fx.Reset();
        var buf2 = AudioTestHelpers.GenerateSineWave(440, 4800, 0.5f);
        fx.Process(buf2.AsSpan());

        Assert.True(AudioTestHelpers.SpansApproxEqual(buf1.AsSpan(), buf2.AsSpan(), 1e-6f),
            "Reset should make processing deterministic");
    }

    [Fact(DisplayName = "DC-006: Parameter clamping")]
    public void DC006_ParameterClamping()
    {
        var fx = new VxDuckingCompressor();

        fx.ThresholdDb = 100f;
        Assert.Equal(0f, fx.ThresholdDb, 0.01f);

        fx.ThresholdDb = -100f;
        Assert.Equal(-60f, fx.ThresholdDb, 0.01f);

        fx.Ratio = 100f;
        Assert.Equal(20f, fx.Ratio, 0.01f);

        fx.Ratio = 0f;
        Assert.Equal(1f, fx.Ratio, 0.01f);

        fx.AttackMs = 0f;
        Assert.Equal(0.5f, fx.AttackMs, 0.01f);

        fx.ReleaseMs = 5000f;
        Assert.Equal(2000f, fx.ReleaseMs, 0.01f);

        fx.RangeDb = 10f;
        Assert.Equal(0f, fx.RangeDb, 0.01f);

        fx.RangeDb = -100f;
        Assert.Equal(-60f, fx.RangeDb, 0.01f);
    }
}
