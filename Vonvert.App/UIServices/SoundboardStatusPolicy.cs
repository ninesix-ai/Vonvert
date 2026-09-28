// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.App.UIServices;

/// <summary>
/// Declarative, UI-free policy behind the soundboard's permanent mode status bar.
///
/// The live/audition split used to be explained by a 4-second toast, which answered a
/// one-off question ("did I just turn this on?") while leaving the ongoing one unanswered
/// ("is anyone else hearing me right now?"). This policy replaces both that toast and the
/// earlier ad-hoc <c>EngineHint</c> visibility check with a single always-visible line:
/// exactly one <see cref="Kind"/> is resolved from the two inputs, and each kind derives
/// its own text and colour tokens. The view therefore carries no conditions of its own -
/// it cannot forget a case, and a new state cannot render as a blank gap.
/// </summary>
public static class SoundboardStatusPolicy
{
    /// <summary>The mutually exclusive states the status bar can report.</summary>
    public enum Kind
    {
        /// <summary>Live mode with a running engine: pads reach the other side.</summary>
        LiveBroadcast,
        /// <summary>Live mode selected but the engine is not running: nobody hears anything.</summary>
        LiveEngineStopped,
        /// <summary>Audition mode: pads stay on the user's own speakers.</summary>
        AuditionLocalOnly,
    }

    /// <summary>Text and colour tokens resolved for one <see cref="Kind"/>.</summary>
    /// <param name="TextKey">Localization key in the <c>ui</c> section.</param>
    /// <param name="AccentToken">Theme.xaml brush key driving the bar's hue language.</param>
    /// <param name="ChipSelector">Style selector the mode chips' highlight keys off.</param>
    public readonly record struct Presentation(string TextKey, string AccentToken, string ChipSelector);

    /// <summary>Alpha applied to the accent colour for the bar's wash background.</summary>
    public const byte BackgroundAlpha = 0x1F;

    /// <summary>
    /// Resolve the state. A stopped engine outranks the broadcast wording: claiming pads
    /// are audible while the engine is down would be a false alarm, and the user's actual
    /// situation ("no one hears anything") is the more actionable fact.
    /// </summary>
    public static Kind Resolve(bool liveMode, bool engineActive)
        => !liveMode                        ? Kind.AuditionLocalOnly
         : !engineActive                    ? Kind.LiveEngineStopped
                                            : Kind.LiveBroadcast;

    /// <summary><see cref="Resolve"/> over the engine's own status enum (only Active is audible).</summary>
    public static Kind For(bool liveMode, EngineStatus status, out Presentation presentation)
    {
        var kind = Resolve(liveMode, status == EngineStatus.Active);
        presentation = Describe(kind);
        return kind;
    }

    /// <summary>Derive the text and colour tokens for a state. Every kind must answer.</summary>
    public static Presentation Describe(Kind kind) => kind switch
    {
        Kind.LiveBroadcast      => new("SoundboardStatusLive",              "Warning", "SbModeLive"),
        Kind.LiveEngineStopped  => new("SoundboardStatusLiveEngineStopped", "Warning", "SbModeLive"),
        Kind.AuditionLocalOnly  => new("SoundboardStatusAudition",          "PowerOn", "SbModeAudition"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown soundboard status"),
    };

    /// <summary>Whether the state is the safe one (drives the reassuring chip hue).</summary>
    public static bool IsLocalOnly(Kind kind) => kind == Kind.AuditionLocalOnly;

    /// <summary>All states, so a test can assert none of them is unworded.</summary>
    public static Kind[] AllKinds => (Kind[])Enum.GetValues(typeof(Kind));
}
