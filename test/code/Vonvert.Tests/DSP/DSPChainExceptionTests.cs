// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the
// DSPChain exception-isolation contract: a throwing effect must never propagate
// out of Process(), must be counted in EngineStats.DspExceptions, must not stop
// sibling effects, and must leave the chain usable after Reset().

namespace Vonvert.Tests.DSP;

using System;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;
using Xunit;

public class DSPChainExceptionTests
{
    [Fact(DisplayName = "DSP-EX-001: Mixed throwing + normal effects — normal effects still process")]
    public void DSP_EX001_MixedEffects_NormalEffectsStillProcess()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);

        var normal1 = new MockEffect { Name = "Normal1", MarkerValue = 42 };
        var thrower = new ThrowingMockEffect { Name = "Thrower" };
        var normal2 = new MockEffect { Name = "Normal2", MarkerValue = 99 };

        chain.Add(normal1); chain.Add(thrower); chain.Add(normal2);

        var buf = new float[64];
        chain.Process(buf);

        Assert.Equal(1, normal1.ProcessCallCount);
        Assert.Equal(1, normal2.ProcessCallCount);
        Assert.Equal(1, thrower.ProcessCallCount);
        Assert.Equal(99f, buf[0]);
        Assert.Equal(1, metrics.DspExceptions);
    }

    [Fact(DisplayName = "DSP-EX-002: Different exception types are all caught")]
    public void DSP_EX002_DifferentExceptionTypes_AllCaught()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);

        chain.Add(new ThrowingMockEffect { Name = "InvalidOp", ExceptionToThrow = new InvalidOperationException("t1") });
        chain.Add(new ThrowingMockEffect { Name = "IndexOutOfRange", ExceptionToThrow = new IndexOutOfRangeException("t2") });
        chain.Add(new ThrowingMockEffect { Name = "DivideByZero", ExceptionToThrow = new DivideByZeroException() });

        var buf = new float[16];
        var ex = Record.Exception(() => chain.Process(buf));

        Assert.Null(ex);
        Assert.Equal(3, metrics.DspExceptions);
    }

    [Fact(DisplayName = "DSP-EX-003: Reset after exceptions — chain is usable again")]
    public void DSP_EX003_ResetAfterExceptions_ChainUsableAgain()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);

        chain.Add(new ThrowingMockEffect { Name = "Thrower" });
        var normal = new MockEffect { Name = "Normal", MarkerValue = 7 };
        chain.Add(normal);

        var buf = new float[32];

        chain.Process(buf);
        Assert.Equal(1, metrics.DspExceptions);

        chain.Reset();

        chain.Process(buf);
        Assert.Equal(2, metrics.DspExceptions);
        Assert.Equal(2, normal.ProcessCallCount);
        Assert.Equal(7f, buf[0]);
    }

    [Fact(DisplayName = "DSP-EX-004: Empty buffer with throwing effects — no crash")]
    public void DSP_EX004_EmptyBuffer_ThrowingEffects_NoCrash()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);
        chain.Add(new ThrowingMockEffect { Name = "T1" });

        var buf = new float[0];
        var ex = Record.Exception(() => chain.Process(buf));
        Assert.Null(ex);
        Assert.Equal(1, metrics.DspExceptions);
    }

    [Fact(DisplayName = "DSP-EX-005: Disabled throwing effect is skipped by the chain")]
    public void DSP_EX005_DisabledThrowingEffect_Skipped()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);
        chain.Add(new ThrowingMockEffect { Name = "DisabledThrower", IsEnabled = false });

        var buf = new float[16];
        chain.Process(buf);

        Assert.Equal(0, ((ThrowingMockEffect)chain.Effects[0]).ProcessCallCount);
        Assert.Equal(0, metrics.DspExceptions);
    }

    [Fact(DisplayName = "DSP-EX-006: 1000 iterations with a throwing effect — metrics accumulate correctly")]
    public void DSP_EX006_1000Iterations_MetricsAccurate()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);
        chain.Add(new ThrowingMockEffect { Name = "Persistent" });

        var buf = new float[8];
        for (int i = 0; i < 1000; i++)
            chain.Process(buf);

        Assert.Equal(1000, metrics.DspExceptions);
    }

    [Fact(DisplayName = "DSP-EX-007: Add/remove a throwing effect between passes — no concurrent-modification crash")]
    public void DSP_EX007_AddRemoveBetweenProcess_NoCrash()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);

        var thrower = new ThrowingMockEffect { Name = "Removable" };
        var normal = new MockEffect { Name = "Stable" };
        chain.Add(thrower); chain.Add(normal);

        var buf = new float[16];
        chain.Process(buf);
        Assert.Equal(1, metrics.DspExceptions);

        chain.Remove(thrower);

        chain.Process(buf);
        Assert.Equal(1, metrics.DspExceptions);
        Assert.Equal(2, normal.ProcessCallCount);
    }

    [Fact(DisplayName = "DSP-EX-008: All effects throw — chain survives 100 iterations")]
    public void DSP_EX008_AllEffectsThrow_ChainSurvives100Iterations()
    {
        var metrics = new EngineStats();
        var chain = new DSPChain(metrics);
        for (int i = 0; i < 5; i++)
            chain.Add(new ThrowingMockEffect { Name = $"Thrower{i}" });

        var buf = new float[32];
        var ex = Record.Exception(() =>
        {
            for (int i = 0; i < 100; i++)
                chain.Process(buf);
        });

        Assert.Null(ex);
        Assert.Equal(500, metrics.DspExceptions);
    }
}
