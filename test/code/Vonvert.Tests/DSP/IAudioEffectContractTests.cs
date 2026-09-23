// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Auto-discovers every public parameterless IAudioEffect in the engine assembly
// and asserts the shared contract, so a newly added effect is held to it without
// editing this file.

namespace Vonvert.Tests.DSP;

using System;
using System.Collections.Generic;
using Vonvert.Engine.DspEngine;
using Vonvert.Tests.Helpers;
using Xunit;

public class IAudioEffectContractTests
{
    /// <summary>
    /// Reflectively discover every public IAudioEffect with a parameterless ctor.
    /// Effects that need a DLL/path argument (none in this build) are skipped
    /// because they have no parameterless constructor. New effects are covered
    /// automatically.
    /// </summary>
    public static IEnumerable<object[]> AllEffects
    {
        get
        {
            var effectInterface = typeof(IAudioEffect);
            foreach (var type in effectInterface.Assembly.GetTypes())
            {
                if (!effectInterface.IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsInterface) continue;
                if (!type.IsPublic) continue;
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null) continue;
                yield return new object[] { (IAudioEffect)ctor.Invoke(null) };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void DSP_001_Name_IsNotNullOrEmpty(IAudioEffect fx)
        => Assert.False(string.IsNullOrEmpty(fx.Name), $"Effect {fx.GetType().Name} has null/empty Name");

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void DSP_002_IsEnabled_RoundTrip(IAudioEffect fx)
    {
        fx.IsEnabled = true;
        Assert.True(fx.IsEnabled);
        fx.IsEnabled = false;
        Assert.False(fx.IsEnabled);
    }

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void DSP_003_Process_PreservesLength(IAudioEffect fx)
    {
        var input = AudioTestHelpers.GenerateWhiteNoise(1024);
        fx.IsEnabled = true;
        fx.Process(input);
        Assert.Equal(1024, input.Length);
    }

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void DSP_004_Process_TenTimes_NoThrow(IAudioEffect fx)
    {
        fx.IsEnabled = true;
        for (int i = 0; i < 10; i++)
        {
            var buf = AudioTestHelpers.GenerateWhiteNoise(1024);
            var ex = Record.Exception(() => fx.Process(buf));
            Assert.Null(ex);
        }
    }

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void DSP_005_Disabled_IsBypass_ViaDSPChain(IAudioEffect fx)
    {
        // Per the IAudioEffect contract the DSPChain performs the enabled check,
        // so a disabled effect added alone must leave the buffer untouched.
        var chain = new DSPChain();
        fx.IsEnabled = false;
        fx.Reset();
        chain.Add(fx);

        var input = AudioTestHelpers.GenerateWhiteNoise(1024);
        var expected = (float[])input.Clone();

        chain.Process(input);

        AssertExtensions.SpanEqual(expected, input);
    }
}
