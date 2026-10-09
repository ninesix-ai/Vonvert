// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// ═══════════════════════════════════════════════════════════════════
//  Preset preview — offline rendering of a voice preset's DSP chain over
//  a pure-code speech-like signal. Proves the ported Synthesis generator
//  and PresetPreviewGenerator behave as the app's preview path expects:
//  deterministic length, peak-normalised output, argument guarding, and
//  that a preset's enabled effects actually change the rendered samples.
// ═══════════════════════════════════════════════════════════════════

namespace Vonvert.Tests.Synthesis;

using System;
using System.Linq;
using Vonvert.Engine.PresetLibrary;
using Vonvert.Engine.Soundboard;
using Vonvert.Engine.Synthesis;
using Xunit;

public sealed class PresetPreviewTests
{
    private const int Rate = 48000;

    [Fact(DisplayName = "SYN-001: Generate returns exactly sampleRate*duration samples")]
    public void SYN001_LengthMatchesDuration()
    {
        var s = SpeechSynthesizer.Generate(Rate, 2.0f);
        Assert.Equal(Rate * 2, s.Length);
    }

    [Fact(DisplayName = "SYN-002: output is peak-normalised to ~1.0 and stays within [-1,1]")]
    public void SYN002_PeakNormalised()
    {
        var s = SpeechSynthesizer.Generate(Rate, 1.0f);
        float peak = s.Max(MathF.Abs);
        Assert.InRange(peak, 0.99f, 1.0001f);
        Assert.All(s, v => Assert.InRange(v, -1.0001f, 1.0001f));
    }

    [Fact(DisplayName = "SYN-003: default call is deterministic (same seed → identical buffer)")]
    public void SYN003_Deterministic()
    {
        var a = SpeechSynthesizer.Generate(Rate, 0.5f);
        var b = SpeechSynthesizer.Generate(Rate, 0.5f);
        Assert.Equal(a, b);
    }

    [Theory(DisplayName = "SYN-004: non-positive sampleRate/duration/baseFreq are rejected")]
    [InlineData(0, 4.0f, 170f)]
    [InlineData(48000, 0f, 170f)]
    [InlineData(48000, 4.0f, 0f)]
    public void SYN004_RejectsInvalidArgs(int rate, float dur, float freq)
    {
        Assert.Throws<ArgumentException>(() => SpeechSynthesizer.Generate(rate, dur, freq));
    }

    [Fact(DisplayName = "SYN-005: GenerateBytes returns float32 PCM (length * 4 bytes)")]
    public void SYN005_GenerateBytes()
    {
        var samples = SpeechSynthesizer.Generate(Rate, 1.0f);
        var bytes = SpeechSynthesizer.GenerateBytes(Rate, 1.0f);
        Assert.Equal(samples.Length * 4, bytes.Length);
    }

    [Fact(DisplayName = "SYN-006: GenerateWithEffects applies each effect in place, in order")]
    public void SYN006_GenerateWithEffects()
    {
        // A gain-of-zero effect must silence the whole buffer, proving the
        // effect chain is actually driven over the synthesized signal.
        var muted = SpeechSynthesizerExtensions.GenerateWithEffects(
            Rate, 0.5f, 170f, new GainEffect(0f));
        Assert.All(muted, v => Assert.Equal(0f, v));

        var boosted = SpeechSynthesizerExtensions.GenerateWithEffects(
            Rate, 0.5f, 170f, new GainEffect(0.5f));
        var raw = SpeechSynthesizer.Generate(Rate, 0.5f);
        Assert.Equal(raw.Length, boosted.Length);
        Assert.True(boosted.Max(MathF.Abs) < raw.Max(MathF.Abs),
            "halving gain should lower the peak");
    }

    [Fact(DisplayName = "PREVIEW-001: null preset throws ArgumentNullException")]
    public void PREVIEW001_NullPresetThrows()
    {
        Assert.Throws<ArgumentNullException>(() => PresetPreviewGenerator.GeneratePreview(null!));
    }

    [Fact(DisplayName = "PREVIEW-002: default profile renders a non-silent, 4s preview")]
    public void PREVIEW002_DefaultProfileRenders()
    {
        var preview = PresetPreviewGenerator.GeneratePreview(new VoiceProfile());
        // 4 s at engine rate.
        Assert.Equal(Rate * 4, preview.Length);
        Assert.True(preview.Any(v => MathF.Abs(v) > 0.01f), "preview should not be silent");
    }

    [Fact(DisplayName = "PREVIEW-003: an enabled effect actually changes the rendered samples")]
    public void PREVIEW003_EnabledEffectChangesOutput()
    {
        var bypass = new VoiceProfile { PreAmpGain = 1.0f };
        var driven = new VoiceProfile { DistortEnabled = true, DistortSaturation = 0.9f };

        var a = PresetPreviewGenerator.GeneratePreview(bypass);
        var b = PresetPreviewGenerator.GeneratePreview(driven);

        Assert.NotEqual(a, b);
    }

    [Fact(DisplayName = "PREVIEW-004: GeneratePreviewBytes returns float32 PCM (length * 4 bytes)")]
    public void PREVIEW004_GeneratePreviewBytes()
    {
        var samples = PresetPreviewGenerator.GeneratePreview(new VoiceProfile());
        var bytes = PresetPreviewGenerator.GeneratePreviewBytes(new VoiceProfile());
        Assert.Equal(samples.Length * 4, bytes.Length);
    }

    private sealed class GainEffect : ISpeechEffect
    {
        private readonly float _gain;
        public GainEffect(float gain) => _gain = gain;
        public void Process(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= _gain;
        }
    }
}
