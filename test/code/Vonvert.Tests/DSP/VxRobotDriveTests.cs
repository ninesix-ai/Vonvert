// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guards for the two effects backing the built-in "Robot" and
// "Demon" presets (ring-modulation timbre and asymmetric soft-clip drive).
// They pin the real-time-contract behaviours the presets rely on: the robot
// modulates a constant tone into an AC waveform without amplifying it, and the
// drive stage hard-limits hot input to the [-1, 1] range while adding
// saturation.  Both must stay silent on silence.

namespace Vonvert.Tests.DSP;

using System;
using Vonvert.Engine.DspEngine;
using Xunit;

public class VxRobotTests
{
    [Fact(DisplayName = "ROBOT-01: ring mod turns a DC tone into AC without gaining")]
    public void Robot_BipolarizesDcAndNeverAmplifies()
    {
        var robot = new VxRobot { IsEnabled = true };   // default 60 Hz carrier
        const int len = 1600;                            // > one 800-sample carrier period
        var buf = new float[len];
        for (int i = 0; i < len; i++) buf[i] = 0.5f;     // constant input

        robot.Process(buf.AsSpan());

        bool sawPos = false, sawNeg = false;
        float peak = 0f;
        foreach (var s in buf)
        {
            if (s > 0f) sawPos = true;
            if (s < 0f) sawNeg = true;
            peak = MathF.Max(peak, MathF.Abs(s));
        }

        Assert.True(sawPos && sawNeg,
            "Robot output must carry both signs — a sine carrier crosses zero every period.");
        Assert.True(peak <= 0.5f + 1e-5f,
            $"Ring mod must only attenuate (|sin| <= 1); peak was {peak:F4} > input 0.5.");
    }

    [Fact(DisplayName = "ROBOT-02: silence in, silence out; effect name contract")]
    public void Robot_SilenceStaysSilent()
    {
        var robot = new VxRobot { IsEnabled = true };
        var buf = new float[512];                        // all zeros
        robot.Process(buf.AsSpan());
        foreach (var s in buf) Assert.Equal(0f, s, 6);

        robot.Reset();
        Assert.Equal("VoiceRobot", robot.Name);          // registry key contract
    }

    [Fact(DisplayName = "ROBOT-03: changing CarrierFreq recomputes the oscillator step")]
    public void Robot_CarrierFreqSetterTakesEffect()
    {
        // Regression guard for the CarrierFreq setter: it must call RecomputeStep(),
        // otherwise two different carriers yield identical output (frequency ignored).
        const int len = 2400;
        var a = new float[len]; System.Array.Fill(a, 0.5f);
        var b = (float[])a.Clone();

        new VxRobot { CarrierFreq = 60f }.Process(a.AsSpan());
        new VxRobot { CarrierFreq = 200f }.Process(b.AsSpan());

        bool anyDiff = false;
        for (int i = 0; i < len && !anyDiff; i++)
            if (MathF.Abs(a[i] - b[i]) > 1e-4f) anyDiff = true;
        Assert.True(anyDiff, "60 Hz and 200 Hz carriers must differ; CarrierFreq setter is inert.");
    }
}

public class VxDriveTests
{
    [Fact(DisplayName = "DRIVE-01: hot sine is soft-clipped into [-1, 1] and distorted")]
    public void Drive_LimitsAndSaturatesHotInput()
    {
        var drive = new VxDrive { IsEnabled = true, Drive = 0.8f };
        const int len = 960;
        var buf = new float[len];
        for (int i = 0; i < len; i++)
            buf[i] = 3f * MathF.Sin(2f * MathF.PI * 100f * i / 48000f);   // well over 0 dBFS

        drive.Process(buf.AsSpan());

        int clamped = 0;
        foreach (var s in buf)
        {
            Assert.True(MathF.Abs(s) <= 1f + 1e-4f,
                $"Drive must never exceed full scale; sample was {s:F4}.");
            if (MathF.Abs(s) > 0.98f) clamped++;
        }
        Assert.True(clamped > 0, "A 3.0-amplitude input must produce saturation (flattened peaks).");
    }

    [Fact(DisplayName = "DRIVE-02: preserves sign and stays silent on silence")]
    public void Drive_MonotonicSignAndSilence()
    {
        var drive = new VxDrive { IsEnabled = true, Drive = 0.5f };
        var positive = new[] { 0.2f };
        drive.Process(positive.AsSpan());
        Assert.True(positive[0] > 0f, "Positive input must stay positive.");

        drive.Reset();
        var neg = new[] { -0.2f };
        drive.Process(neg.AsSpan());
        Assert.True(neg[0] < 0f, "Negative input must stay negative (asymmetric curve, no inversion).");

        var silence = new float[256];
        drive.Process(silence.AsSpan());
        foreach (var s in silence) Assert.Equal(0f, s, 6);
    }
}
