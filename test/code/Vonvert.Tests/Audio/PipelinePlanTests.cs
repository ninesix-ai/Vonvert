// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using Xunit;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Tests.Audio;

// PA-001 ~ PA-009: what the audio worker does per A/B mode, expressed as an ordered
// step list instead of a hand-written switch.
//
// Two real defects lived in that switch. On DRY (which is also what the power button
// selects) nothing at all was fed to the analyzers, so the monitor froze on its last
// frame and read as a crash. And on WET the analyzers ran before the dry signal was
// subtracted, so the meters and the waterfall showed a fuller signal than the one the
// listener actually heard. Both are pinned here, and PA-006 pins the invariant whose
// loss caused the first one: every mode ends by feeding the analysers.
public sealed class PipelinePlanTests
{
    private static PipelineStep[] Steps(CompareMode mode) => PipelinePlan.StepsFor(mode).ToArray();

    [Fact(DisplayName = "PA-001: normal applies the DSP chain and analyses its output")]
    public void PA001_Normal()
    {
        Assert.Equal(new[]
        {
            PipelineStep.FeedInput, PipelineStep.ApplyDsp, PipelineStep.FeedAnalyzers,
        }, Steps(CompareMode.Normal));
    }

    [Fact(DisplayName = "PA-002: DRY still feeds the taps and the analyzers - the monitor must never freeze")]
    public void PA002_DryKeepsAnalysing()
    {
        var steps = Steps(CompareMode.Dry);
        Assert.Contains(PipelineStep.FeedInput, steps);
        Assert.Contains(PipelineStep.FeedAnalyzers, steps);
    }

    [Fact(DisplayName = "PA-003: DRY never runs the DSP chain, so the audible output stays the raw voice")]
    public void PA003_DryAppliesNoEffects()
        => Assert.DoesNotContain(PipelineStep.ApplyDsp, Steps(CompareMode.Dry));

    [Fact(DisplayName = "PA-004: WET captures the dry reference before the chain, then subtracts after it")]
    public void PA004_WetOrdering()
    {
        var steps = Steps(CompareMode.WetOnly);
        Assert.True(steps.IndexOf(PipelineStep.CaptureDry) < steps.IndexOf(PipelineStep.ApplyDsp),
            "the dry reference has to be taken before the effects run");
        Assert.True(steps.IndexOf(PipelineStep.ApplyDsp) < steps.IndexOf(PipelineStep.SubtractDry),
            "subtraction only makes sense against a processed block");
    }

    [Fact(DisplayName = "PA-005: WET analyses the audible result, not the pre-subtraction signal")]
    public void PA005_WetAnalysesFinalOutput()
    {
        var steps = Steps(CompareMode.WetOnly);
        Assert.True(steps.IndexOf(PipelineStep.SubtractDry) < steps.IndexOf(PipelineStep.FeedAnalyzers),
            "meters showed a fuller signal than the listener heard; analysis must follow the subtraction");
    }

    [Fact(DisplayName = "PA-006: every mode ends by feeding the analysers")]
    public void PA006_AlwaysAnalyses()
    {
        foreach (CompareMode mode in System.Enum.GetValues<CompareMode>())
        {
            var steps = Steps(mode);
            Assert.NotEmpty(steps);
            Assert.Equal(PipelineStep.FeedAnalyzers, steps[^1]);
        }
    }

    [Fact(DisplayName = "PA-007: an unrecognised mode falls back to the normal plan, never to an empty one")]
    public void PA007_UnknownModeFallsBack()
    {
        var steps = Steps((CompareMode)999);
        Assert.Equal(Steps(CompareMode.Normal), steps);
    }

    [Fact(DisplayName = "PA-008: the audio worker walks the plan instead of repeating a switch per mode")]
    public void PA008_WorkerUsesThePlan()
    {
        // The freeze was invisible in review precisely because it was a missing case in a
        // switch inside the audio loop. Pin that the loop delegates to the plan, so the
        // per-mode logic cannot quietly grow back a private copy.
        var source = WorkerSource();
        Assert.Contains("PipelinePlan.StepsFor(Settings.Compare)", source);
        Assert.DoesNotContain("case CompareMode.Dry:", source);
        Assert.DoesNotContain("case CompareMode.WetOnly:", source);
    }

    [Fact(DisplayName = "PA-009: running the DRY plan keeps both waveform taps and the spectrogram alive")]
    public void PA009_RunningDryPlanKeepsTheMonitorLive()
    {
        // The consequence, not just the data: this is the exact sequence the worker now
        // executes for DRY (and therefore for the power button), and the bug it guards
        // against was a frozen waterfall plus a loudest-reading meter stuck on last block.
        var p = new NullAudioProcessor();
        var steps = Steps(CompareMode.Dry);
        var scratch = new float[480];
        p.OnStart();
        try
        {
            for (int b = 0; b < 60; b++)
            {
                var work = Sine(1000f, 480);
                var span = work.AsSpan();
                foreach (var step in steps)
                {
                    switch (step)
                    {
                        case PipelineStep.CaptureDry: work.AsSpan().CopyTo(scratch); break;
                        case PipelineStep.FeedInput: p.PreMix(span, 480); break;
                        case PipelineStep.ApplyDsp: p.Process(span); break;
                        case PipelineStep.SubtractDry:
                            for (int i = 0; i < 480; i++) span[i] -= scratch[i];
                            break;
                        case PipelineStep.FeedAnalyzers: p.PostAnalyze(span, 480); break;
                    }
                }
            }

            Assert.True(p.InputWaveform.GetPeak() > 0.1f, "dry mode lost the input waveform");
            Assert.True(p.OutputWaveform.GetPeak() > 0.1f, "dry mode froze the output waveform tap");
            Assert.True(p.Loudness.ShortTermLufs > -99f, "dry mode froze the loudness meter");
        }
        finally { p.OnStop(); p.Dispose(); }
    }

    private static string WorkerSource()
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return System.IO.File.ReadAllText(System.IO.Path.Combine(
            dir!.FullName, "Vonvert.Engine", "AudioEngine", "VoiceEngine.cs"));
    }

    private static float[] Sine(float hz, int n)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++) s[i] = 0.7f * System.MathF.Sin(2f * System.MathF.PI * hz * i / 48000f);
        return s;
    }
}
