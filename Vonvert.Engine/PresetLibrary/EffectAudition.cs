// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Reflection;
using Vonvert.Engine.AudioEngine;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.Synthesis;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Builds a one-effect audition clip: a short speech-like signal rendered through
/// ONLY the selected effect, using the parameter values carried by the profile.
/// Local-only — never touches the live engine or the microphone. UI-free by design,
/// so the same generator can be driven from the expert panel and unit tests alike.
/// </summary>
public static class EffectAudition
{
    /// <summary>Length of the synthesized demo clip, in seconds.</summary>
    public const float DurationSec = 3.5f;

    private const int SampleRate = AudioConstants.EngineRate;

    /// <summary>
    /// Build a chain containing the single <paramref name="keep"/> effect (at the
    /// profile's current values) plus the unconditional safety stages, and nothing
    /// else. Every <c>*Enabled</c> flag is forced off first so a future effect
    /// cannot leak into the audition and the source profile is left untouched.
    /// </summary>
    /// <exception cref="ArgumentException">The group owns no switchable effect (e.g. pure gain).</exception>
    public static DSPChain BuildSingleEffectChain(VoiceProfile profile, DspGroup keep)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(keep);

        // Only groups that own a switchable effect can be auditioned.
        if (keep.SetEnable is null)
            throw new ArgumentException($"Group '{keep.NameKey}' has no effect to audition.", nameof(keep));

        var clone = profile.Clone();

        // Disable every effect the profile knows about. Reflection (rather than a
        // hand-kept list) means a newly added effect cannot silently stay on.
        foreach (var prop in typeof(VoiceProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType == typeof(bool)
                && prop.Name.EndsWith("Enabled", StringComparison.Ordinal)
                && prop.CanWrite)
            {
                prop.SetValue(clone, false);
            }
        }

        // No VST loading and no custom routing during an audition.
        clone.VstPlugins.Clear();
        clone.EffectOrder.Clear();

        // Re-enable exactly the requested effect at its current values.
        keep.SetEnable(clone, true);

        return clone.CreateDSPChain();
    }

    /// <summary>Generate the audition clip as IEEE float32 PCM bytes (mono, at
    /// <see cref="AudioConstants.EngineRate"/>), ready for
    /// <see cref="IAuditionPlayer.Play"/>.</summary>
    public static byte[] GeneratePreviewBytes(VoiceProfile profile, DspGroup keep)
    {
        var samples = SpeechSynthesizer.Generate(SampleRate, DurationSec);
        var chain = BuildSingleEffectChain(profile, keep);
        chain.Process(samples.AsSpan());

        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
