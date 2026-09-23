// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Structural guard for the chain container and its default wiring, plus
// exception isolation via EngineStats.

namespace Vonvert.Tests.DSP;

using System;
using System.Linq;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Tests.Helpers;
using Xunit;

public class DSPChainTests
{
    [Fact]
    public void CreateDefault_EffectCount()
    {
        var chain = DSPChain.CreateDefault();
        // Floor guards accidental removals without breaking on every added effect.
        Assert.True(chain.Effects.Count >= 10, $"expected >= 10 effects, got {chain.Effects.Count}");
    }

    [Fact]
    public void CreateDefault_DefaultEnabledEffects()
    {
        var chain = DSPChain.CreateDefault();
        Assert.True(chain.Effects.OfType<VxGate>().Single().IsEnabled);
        Assert.True(chain.Effects.OfType<VxCompressor>().Single().IsEnabled);
        Assert.True(chain.Effects.OfType<VxLimiter>().Single().IsEnabled);
    }

    [Fact]
    public void CreateDefault_Order_FirstFour()
    {
        var chain = DSPChain.CreateDefault();
        Assert.IsType<DCOffsetFilter>(chain.Effects[0]);
        Assert.IsType<VxGate>(chain.Effects[1]);
        Assert.IsType<NoiseReductionEffect>(chain.Effects[2]);
        Assert.IsType<VxPitch>(chain.Effects[3]);
    }

    [Fact]
    public void CreateDefault_Order_LastIsLoudnessMeter()
    {
        var chain = DSPChain.CreateDefault();
        Assert.IsType<LoudnessMeterEffect>(chain.Effects[^1]);
        Assert.DoesNotContain(chain.Effects, e => e.Name.Contains("Watermark", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Add_Remove_Clear()
    {
        var chain = new DSPChain();
        var fx1 = new MockEffect { Name = "A" };
        var fx2 = new MockEffect { Name = "B" };
        chain.Add(fx1);
        chain.Add(fx2);
        Assert.Equal(2, chain.Effects.Count);
        chain.Remove(fx1);
        Assert.Single(chain.Effects);
        Assert.Same(fx2, chain.Effects[0]);
        chain.Clear();
        Assert.Empty(chain.Effects);
    }

    [Fact]
    public void Reset_CallsAllEffectsReset()
    {
        var chain = new DSPChain();
        var mocks = new[] { new MockEffect(), new MockEffect(), new MockEffect() };
        foreach (var m in mocks) chain.Add(m);
        chain.Reset();
        foreach (var m in mocks) Assert.Equal(1, m.ResetCallCount);
    }

    [Fact]
    public void Process_ExecutesInOrder_MarkerValues()
    {
        var chain = new DSPChain();
        chain.Add(new MockEffect { Name = "M1", MarkerValue = 1 });
        chain.Add(new MockEffect { Name = "M2", MarkerValue = 2 });
        chain.Add(new MockEffect { Name = "M3", MarkerValue = 3 });
        var buf = new float[128];
        chain.Process(buf);
        Assert.Equal(3f, buf[0]);
    }

    [Fact]
    public void Process_OnlyEnabledEffectsRun()
    {
        var chain = new DSPChain();
        var m1 = new MockEffect { Name = "M1", MarkerValue = 1, IsEnabled = false };
        var m2 = new MockEffect { Name = "M2", MarkerValue = 2, IsEnabled = true };
        var m3 = new MockEffect { Name = "M3", MarkerValue = 3, IsEnabled = false };
        chain.Add(m1); chain.Add(m2); chain.Add(m3);
        var buf = new float[128];
        chain.Process(buf);
        Assert.Equal(2f, buf[0]);
        Assert.Equal(0, m1.ProcessCallCount);
        Assert.Equal(1, m2.ProcessCallCount);
        Assert.Equal(0, m3.ProcessCallCount);
    }

    [Fact]
    public void Process_AccumulatesDspExceptions_WithMetrics()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);
        chain.Add(new ThrowingMockEffect { Name = "T1" });
        chain.Add(new ThrowingMockEffect { Name = "T2" });
        chain.Add(new ThrowingMockEffect { Name = "T3" });
        for (int i = 0; i < 100; i++)
        {
            var buf = new float[16];
            var ex = Record.Exception(() => chain.Process(buf));
            Assert.Null(ex);
        }
        Assert.Equal(300, metrics.DspExceptions);
    }

    [Fact]
    public void Process_NoMetrics_ExceptionsNotPropagated()
    {
        var chain = new DSPChain();
        chain.Add(new ThrowingMockEffect { Name = "T1" });
        var buf = new float[16];
        var ex = Record.Exception(() => chain.Process(buf));
        Assert.Null(ex);
    }
}
