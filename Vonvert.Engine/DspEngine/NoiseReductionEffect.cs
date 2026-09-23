// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.Engine.DspEngine;

/*
 * MMSE spectral gain noise reducer.
 * Preserves the full public parameter API so that VoiceProfile,
 * DSPChain factory, and the UI can reference it uniformly.
 */
public sealed class NoiseReductionEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceNoiseReduce";
    public bool   IsEnabled { get; set; }

    private float _strength = 2.0f;
    private float _floor = 0.01f;
    private int _learnFrames = 30;
    private float _perceptualStrength = 0.7f;
    private float _suppressionFloorDb = -20f;

    public float Strength
    {
        get => _strength;
        set => _strength = Math.Clamp(value, 1f, 4f);
    }

    public float Floor
    {
        get => _floor;
        set => _floor = Math.Clamp(value, 0.001f, 0.1f);
    }

    public int LearnFrames
    {
        get => _learnFrames;
        set => _learnFrames = Math.Clamp(value, 10, 100);
    }

    public float PerceptualStrength
    {
        get => _perceptualStrength;
        set => _perceptualStrength = Math.Clamp(value, 0f, 1f);
    }

    public float SuppressionFloorDb
    {
        get => _suppressionFloorDb;
        set => _suppressionFloorDb = Math.Clamp(value, -60f, 0f);
    }

    public bool ProfileReady => false;
    public int LearnProgress => 0;

    public void StartLearning() { }

    // Pass-through: no processing in OSS stub
    public void Process(Span<float> buffer) { }

    public void Reset() { }
}
