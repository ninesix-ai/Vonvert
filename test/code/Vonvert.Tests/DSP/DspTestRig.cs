// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Shared construction of a live VoiceEngine plus the parameter coordinator that
// maps to its chain instances. Wiring tests use this instead of repeating the
// 21-argument coordinator constructor.

namespace Vonvert.Tests.DSP;

using System.Linq;
using Vonvert.App;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Dynamics;
using Vonvert.Engine.DspEngine.Pitch;
using Vonvert.Engine.DspEngine.TimeBased;

public static class DspTestRig
{
    public static (VoiceEngine engine, DspParameterCoordinator coord) New()
    {
        var engine = new VoiceEngine();
        var fx = engine.Effects.Effects;
        var coord = new DspParameterCoordinator(
            engine,
            fx.OfType<VxPitch>().Single(), fx.OfType<VxReverb>().Single(), fx.OfType<VxChorus>().Single(),
            fx.OfType<VxCompressor>().Single(), fx.OfType<VxGate>().Single(), fx.OfType<VxEq>().Single(),
            fx.OfType<NoiseReductionEffect>().Single(), fx.OfType<DelayEffect>().Single(), fx.OfType<DeesserEffect>().Single(),
            fx.OfType<VxRobot>().Single(), fx.OfType<VxDrive>().Single(),
            fx.OfType<TiltEQEffect>().Single(), fx.OfType<GraphicEQEffect>().Single(), fx.OfType<BitcrusherEffect>().Single(),
            fx.OfType<FlangerEffect>().Single(), fx.OfType<PhaserEffect>().Single(), fx.OfType<TremoloEffect>().Single(), fx.OfType<VibratoEffect>().Single(),
            fx.OfType<RingModEffect>().Single(), fx.OfType<LoFiReverbEffect>().Single(), fx.OfType<ModulationDelayEffect>().Single());
        return (engine, coord);
    }
}
