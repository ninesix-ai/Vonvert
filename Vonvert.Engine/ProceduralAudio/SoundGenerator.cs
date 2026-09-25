// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Engine.ProceduralAudio;
/// <summary>
/// Procedurally generates audio samples for all built-in sound effects.
/// All output is 48 kHz mono IEEE float32 PCM.
/// Zero dependencies, cross-platform.
///
/// Generation methods are split into partial class files by category:
///   SoundGenerator.Drums.cs, SoundGenerator.Tones.cs, SoundGenerator.Sfx.cs,
///   SoundGenerator.Memes.cs, SoundGenerator.Music.cs, SoundGenerator.Retro.cs,
///   SoundGenerator.Ambient.cs
/// </summary>
public static partial class SoundGenerator
{
    private const int DefaultRate = 48000;

    // ── Sound Catalogue ──────────────────────────────────────────────────
    private static readonly SoundDefinition[] Builtins =
    [
        // Drums
        new("kick",      "Kick",       "Drums",  "\U0001F941", SoundSourceType.Generated),
        new("snare",     "Snare",      "Drums",  "\U0001FA98", SoundSourceType.Generated),
        new("hihat",     "Hi-Hat",     "Drums",  "\U0001F3A9", SoundSourceType.Generated),
        new("clap",      "Clap",       "Drums",  "\U0001F44F", SoundSourceType.Generated),
        new("tom",       "Tom",        "Drums",  "\U0001F945", SoundSourceType.Generated),
        new("rimshot",   "Rimshot",    "Drums",  "\U0001FA91", SoundSourceType.Generated),
        new("drumroll",  "Drum Roll",  "Drums",  "\U0001F941", SoundSourceType.Generated),
        new("crash",     "Crash",      "Drums",  "\U0001F4A5", SoundSourceType.Generated),
        new("gong",      "Gong",       "Drums",  "\U0001F3AD", SoundSourceType.Generated),
        // Tones
        new("sine",      "Sine",       "Tones",  "\u3030\uFE0F",  SoundSourceType.Generated),
        new("square",    "Square",     "Tones",  "\u25FB",   SoundSourceType.Generated),
        new("saw",       "Saw",        "Tones",  "\U0001F4D0", SoundSourceType.Generated),
        new("bass_drop", "Bass Drop",  "Tones",  "\U0001F4C9", SoundSourceType.Generated),
        new("sub_boom",  "Sub Boom",   "Tones",  "\U0001F31A", SoundSourceType.Generated),
        // SFX
        new("rise",      "Rise",       "SFX",    "\u2B06",   SoundSourceType.Generated),
        new("fall",      "Fall",       "SFX",    "\u2B07",   SoundSourceType.Generated),
        new("laser",     "Laser",      "SFX",    "\u26A1",   SoundSourceType.Generated),
        new("siren",     "Siren",      "SFX",    "\U0001F6A8", SoundSourceType.Generated),
        new("alarm",     "Alarm",      "SFX",    "\U0001F514", SoundSourceType.Generated),
        new("powerup",   "Power Up",   "SFX",    "\u2728",   SoundSourceType.Generated),
        new("whoosh",    "Whoosh",     "SFX",    "\U0001F32C", SoundSourceType.Generated),
        new("explosion", "Explosion",  "SFX",    "\U0001F9BE", SoundSourceType.Generated),
        new("heartbeat", "Heartbeat",  "SFX",    "\U0001F493", SoundSourceType.Generated),
        new("knock",     "Door Knock", "SFX",    "\U0001F6AA", SoundSourceType.Generated),
        new("glass",     "Glass Break","SFX",    "\U0001F95B", SoundSourceType.Generated),
        new("teleport",  "Teleport",   "SFX",    "\U0001F300", SoundSourceType.Generated),
        new("buzzer",    "Wrong Buzz", "SFX",    "\u26D4",   SoundSourceType.Generated),
        // Memes
        new("airhorn",   "Air Horn",   "Memes",  "\U0001F4E2", SoundSourceType.Generated),
        new("sad_trombone", "Sad Trombone", "Memes", "\U0001F4E9", SoundSourceType.Generated),
        new("tada",      "Ta-DA",      "Memes",  "\U0001F389", SoundSourceType.Generated),
        new("dun_dun_dun", "Dun Dun Dun", "Memes", "\U0001F3AC", SoundSourceType.Generated),
        new("boing",     "Boing",      "Memes",  "\U0001F4A6", SoundSourceType.Generated),
        // Music
        new("cmaj_scale", "C Major Scale", "Music", "\U0001F3B5", SoundSourceType.Generated),
        new("chord_maj",  "Major Chord",   "Music", "\U0001F3B6", SoundSourceType.Generated),
        new("chord_min",  "Minor Chord",   "Music", "\U0001F3BC", SoundSourceType.Generated),
        new("arp",        "Arpeggio",      "Music", "\U0001F58B", SoundSourceType.Generated),
        new("bass_line",  "Bass Line",     "Music", "\U0001F3B8", SoundSourceType.Generated),
        new("bell",       "Bell Chime",    "Music", "\U0001F408", SoundSourceType.Generated),
        // Retro (8-bit game sounds)
        new("coin",       "Coin",        "Retro", "\U0001F4B0", SoundSourceType.Generated),
        new("jump",       "Jump",        "Retro", "\U0001F437", SoundSourceType.Generated),
        new("blip",       "Blip",        "Retro", "\U0001F579", SoundSourceType.Generated),
        new("game_over",  "Game Over",   "Retro", "\U0001F3AE", SoundSourceType.Generated),
        new("level_up",   "Level Up",    "Retro", "\U0001F31F", SoundSourceType.Generated),
        // Ambient
        new("white_noise", "White Noise", "Ambient", "\u2591",  SoundSourceType.Generated),
        new("pink_noise",  "Pink Noise",  "Ambient", "\U0001F7E5", SoundSourceType.Generated),
        new("brown_noise", "Brown Noise", "Ambient", "\U0001F7EB", SoundSourceType.Generated),
        new("rain",        "Rain",        "Ambient", "\U0001F327", SoundSourceType.Generated),
        new("thunder",     "Thunder",     "Ambient", "\U0001F329", SoundSourceType.Generated),
        new("ocean",       "Ocean Waves", "Ambient", "\U0001F30A", SoundSourceType.Generated),
        new("campfire",    "Campfire",    "Ambient", "\U0001F525", SoundSourceType.Generated),
    ];
    private static readonly string[] CategoryOrder = ["Drums", "Tones", "SFX", "Memes", "Music", "Retro", "Ambient"];

    // ── Public API ───────────────────────────────────────────────────────
    /// <summary>Returns all built-in sound definitions.</summary>

    public static IReadOnlyList<SoundDefinition> GetAllSounds() => Builtins;
    /// <summary>Returns sounds filtered by category.</summary>

    public static IReadOnlyList<SoundDefinition> GetByCategory(string category)
        => Builtins.Where(s => s.Category == category).ToArray();
    /// <summary>Returns category names in display order.</summary>

    public static IReadOnlyList<string> GetCategories() => CategoryOrder;
    /// <summary>Generates float32 samples at the given sample rate.</summary>

    public static float[] Generate(string soundId, int sampleRate = DefaultRate)
    {
        return soundId switch
        {
            "kick"          => GenKick(sampleRate),
            "snare"         => GenSnare(sampleRate),
            "hihat"         => GenHiHat(sampleRate),
            "clap"          => GenClap(sampleRate),
            "tom"           => GenTom(sampleRate),
            "rimshot"       => GenRimshot(sampleRate),
            "drumroll"      => GenDrumroll(sampleRate),
            "crash"         => GenCrash(sampleRate),
            "gong"          => GenGong(sampleRate),
            "sine"          => GenTone(sampleRate, 440f, WaveShape.Sine, 0.5f),
            "square"        => GenTone(sampleRate, 440f, WaveShape.Square, 0.5f),
            "saw"           => GenTone(sampleRate, 440f, WaveShape.Saw, 0.5f),
            "bass_drop"     => GenBassDrop(sampleRate),
            "sub_boom"      => GenSubBoom(sampleRate),
            "rise"          => GenSweep(sampleRate, 200f, 2000f, 0.8f),
            "fall"          => GenSweep(sampleRate, 2000f, 200f, 0.8f),
            "laser"         => GenLaser(sampleRate),
            "siren"         => GenSiren(sampleRate),
            "alarm"         => GenAlarm(sampleRate),
            "powerup"       => GenPowerUp(sampleRate),
            "whoosh"        => GenWhoosh(sampleRate),
            "explosion"     => GenExplosion(sampleRate),
            "heartbeat"     => GenHeartbeat(sampleRate),
            "knock"         => GenKnock(sampleRate),
            "glass"         => GenGlassBreak(sampleRate),
            "teleport"      => GenTeleport(sampleRate),
            "buzzer"        => GenBuzzer(sampleRate),
            "airhorn"       => GenAirHorn(sampleRate),
            "sad_trombone"  => GenSadTrombone(sampleRate),
            "tada"          => GenTaDa(sampleRate),
            "dun_dun_dun"   => GenDunDunDun(sampleRate),
            "boing"         => GenBoing(sampleRate),
            "cmaj_scale"    => GenCMajorScale(sampleRate),
            "chord_maj"     => GenChord(sampleRate, new[] { 261.63f, 329.63f, 392.00f }, 1.0f),
            "chord_min"     => GenChord(sampleRate, new[] { 220.00f, 261.63f, 329.63f }, 1.0f),
            "arp"           => GenArpeggio(sampleRate),
            "bass_line"     => GenBassLine(sampleRate),
            "bell"          => GenBell(sampleRate),
            "coin"          => GenCoin(sampleRate),
            "jump"          => GenJump(sampleRate),
            "blip"          => GenBlip(sampleRate),
            "game_over"     => GenGameOver(sampleRate),
            "level_up"      => GenLevelUp(sampleRate),
            "white_noise"   => GenWhiteNoise(sampleRate, 2.0f),
            "pink_noise"    => GenPinkNoise(sampleRate, 2.0f),
            "brown_noise"   => GenBrownNoise(sampleRate, 2.0f),
            "rain"          => GenRain(sampleRate),
            "thunder"       => GenThunder(sampleRate),
            "ocean"         => GenOcean(sampleRate),
            "campfire"      => GenCampfire(sampleRate),
            _               => Array.Empty<float>()
        };
    }
    /// <summary>Generates and converts to IEEE float32 PCM byte array.</summary>

    public static byte[] GenerateBytes(string soundId, int sampleRate = DefaultRate)
    {
        var samples = Generate(soundId, sampleRate);
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    // ── Shared helpers (used by category partial files) ──────────────────
    private enum WaveShape { Sine, Square, Saw }

    private static float ExpDecay(float t, float tauMs, float durationMs)
    {
        if (t < 0 || t > durationMs) return 0f;
        return MathF.Exp(-t / tauMs);
    }

    private static float FadeInOut(float t, float totalMs, float fadeMs)
    {
        if (t < fadeMs) return t / fadeMs;
        if (t > totalMs - fadeMs) return MathF.Max(0f, (totalMs - t) / fadeMs);
        return 1f;
    }

    private static float[] GenTone(int sr, float freq, WaveShape shape, float durationSec)
    {
        int n = (int)(sr * durationSec);
        var buf = new float[n];
        float fade = sr * 0.005f; // 5ms fade
        float total = n;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = FadeInOut(i, total, fade);
            float s = shape switch
            {
                WaveShape.Sine   => MathF.Sin(2f * MathF.PI * freq * t),
                WaveShape.Square => MathF.Sin(2f * MathF.PI * freq * t) >= 0 ? 1f : -1f,
                WaveShape.Saw    => 2f * (freq * t % 1f) - 1f,
                _ => 0f
            };
            buf[i] = s * env * 0.7f;
        }
        return buf;
    }

    private static void WriteNote(float[] buf, int sr, int startSample, float durSec, float freq)
    {
        int noteSamples = (int)(sr * durSec);
        float phase = 0f;
        for (int j = 0; j < noteSamples && startSample + j < buf.Length; j++)
        {
            float env = FadeInOut(j, noteSamples, sr * 0.01f);
            phase += 2f * MathF.PI * freq / sr;
            buf[startSample + j] += MathF.Sin(phase) * env * 0.6f;
        }
    }
}
