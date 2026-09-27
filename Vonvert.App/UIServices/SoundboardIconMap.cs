// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System.Collections.Generic;

namespace Vonvert.App.UIServices;

/// <summary>Maps built-in soundboard ids to the category-colored vector glyphs defined
/// in Assets/Icons.xaml (x:Key "IcSb&lt;Name&gt;"). WPF layered windows cannot render
/// COLR color emoji, so pads use these vectors instead of the legacy Emoji text.</summary>
public static class SoundboardIconMap
{
    /// <summary>Icon resource key for unknown ids (question-mark glyph).</summary>
    public const string UnknownKey = "IcSbUnknown";

    /// <summary>Icon resource key for user-imported pads (note in brand accent).</summary>
    public const string UserKey = "IcSbUser";

    private static readonly Dictionary<string, string> ById = new()
    {
        // Drums
        ["kick"] = "IcSbKick", ["drumroll"] = "IcSbKick",
        ["snare"] = "IcSbSnare", ["hihat"] = "IcSbHiHat",
        ["clap"] = "IcSbClap", ["tom"] = "IcSbTom",
        ["rimshot"] = "IcSbCymbal", ["crash"] = "IcSbCymbal", ["gong"] = "IcSbGong",
        // Tones
        ["sine"] = "IcSbSine", ["square"] = "IcSbSquare", ["saw"] = "IcSbSaw",
        ["bass_drop"] = "IcSbDecline", ["sub_boom"] = "IcSbRiseWave",
        // SFX
        ["laser"] = "IcSbBolt", ["siren"] = "IcSbSiren", ["alarm"] = "IcSbBell",
        ["powerup"] = "IcSbSparkle", ["whoosh"] = "IcSbDroplet",
        ["explosion"] = "IcSbHeart", ["heartbeat"] = "IcSbHeart",
        ["knock"] = "IcSbDoor", ["glass"] = "IcSbCrack",
        ["teleport"] = "IcSbVortex", ["buzzer"] = "IcSbNoEntry",
        ["fall"] = "IcSbDecline", ["rise"] = "IcSbJump",
        // Memes
        ["airhorn"] = "IcSbMegaphone", ["sad_trombone"] = "IcSbTrombone",
        ["tada"] = "IcSbParty", ["dun_dun_dun"] = "IcSbTape", ["boing"] = "IcSbSweat",
        // Music
        ["cmaj_scale"] = "IcSbKeys", ["chord_maj"] = "IcSbNote",
        ["chord_min"] = "IcSbNote", ["arp"] = "IcSbPen",
        ["bass_line"] = "IcSbGuitar", ["bell"] = "IcSbHiHat",
        // Retro
        ["coin"] = "IcSbCoin", ["jump"] = "IcSbJump", ["blip"] = "IcSbScreen",
        ["game_over"] = "IcSbPad", ["level_up"] = "IcSbStar",
        // Ambient
        ["white_noise"] = "IcSbDotHollow", ["pink_noise"] = "IcSbDotFilled",
        ["brown_noise"] = "IcSbDotFilled", ["rain"] = "IcSbRain",
        ["thunder"] = "IcSbThunder", ["ocean"] = "IcSbWave", ["campfire"] = "IcSbFlame",
    };

    /// <summary>Resource key of the glyph for one pad: exact id match first, then the
    /// category fallback, then the user-imported note, then the unknown placeholder.</summary>
    public static string Resolve(string soundId, string category, bool isUserImported)
    {
        if (ById.TryGetValue(soundId, out var key)) return key;
        if (isUserImported) return UserKey;
        return category switch
        {
            "Drums"   => "IcSbKick",
            "Tones"   => "IcSbSine",
            "SFX"     => "IcSbBolt",
            "Memes"   => "IcSbMegaphone",
            "Music"   => "IcSbNote",
            "Retro"   => "IcSbCoin",
            "Ambient" => "IcSbWave",
            _         => UnknownKey,
        };
    }
}
