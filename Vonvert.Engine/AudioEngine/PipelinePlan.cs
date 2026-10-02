// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections.Generic;

namespace Vonvert.Engine.AudioEngine;

/// <summary>One stage of the per-block audio worker pipeline.</summary>
public enum PipelineStep
{
    /// <summary>Copy the incoming block aside so WET mode can subtract it later.</summary>
    CaptureDry,
    /// <summary>Dry waveform tap and the original-mode recorder.</summary>
    FeedInput,
    /// <summary>Run the effect chain over the block.</summary>
    ApplyDsp,
    /// <summary>WET mode: remove the dry reference, leaving only what the effects added.</summary>
    SubtractDry,
    /// <summary>Wet waveform tap, the analyzer pump and the processed-mode recorder.</summary>
    FeedAnalyzers,
}

/// <summary>
/// Which stages run, and in what order, for each A/B mode.
///
/// This used to be a hand-written switch inside the audio worker, and it lost two
/// behaviours that only show up on screen: on DRY - which is also what the power button
/// selects - no stage ran at all, so the fullscreen monitor kept painting its last frame
/// and looked like a crash; and on WET the analysers were fed before the dry signal was
/// subtracted, so the meters showed a fuller signal than the one listeners heard. As
/// data, both are invariants a test can hold (PA-001 ~ PA-007), including "every mode
/// ends by feeding the analysers".
/// </summary>
public static class PipelinePlan
{
    private static readonly PipelineStep[] NormalSteps =
    {
        PipelineStep.FeedInput, PipelineStep.ApplyDsp, PipelineStep.FeedAnalyzers,
    };

    /// <summary>Raw voice to the output, and to the meters - no DSP, but never blind.</summary>
    private static readonly PipelineStep[] DrySteps =
    {
        PipelineStep.FeedInput, PipelineStep.FeedAnalyzers,
    };

    private static readonly PipelineStep[] WetSteps =
    {
        PipelineStep.CaptureDry, PipelineStep.FeedInput, PipelineStep.ApplyDsp,
        PipelineStep.SubtractDry, PipelineStep.FeedAnalyzers,
    };

    /// <summary>
    /// Static arrays: the worker calls this once per audio block, so building a list here
    /// would put an allocation on the real-time path.
    /// </summary>
    public static IReadOnlyList<PipelineStep> StepsFor(CompareMode mode) => mode switch
    {
        CompareMode.Dry     => DrySteps,
        CompareMode.WetOnly => WetSteps,
        _                   => NormalSteps,     // unknown values behave like Normal, not like silence
    };
}
