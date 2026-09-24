// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vonvert.Engine.Services;

/// <summary>End-user roles that tailor the UI to a use case.</summary>
public enum PersonaType { Beginner, Gamer, Streamer, Creator, VoiceActor, AudioEngineer, SocialVoice, AiEnthusiast }

/// <summary>Four-tier UI complexity (L1 minimal / L2 standard / L3 advanced / L4 professional).</summary>
public enum ComplexityLevel { Minimal = 1, Standard = 2, Advanced = 3, Professional = 4 }

/// <summary>Three broad onboarding entry groups.</summary>
public enum PersonaGroup { Entertainment, ContentCreation, ProfessionalAudio }

/// <summary>Static per-role metadata.</summary>
public sealed class PersonaDescriptor
{
    public PersonaType Type { get; init; }
    public string NameKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";
    public string IconKey { get; init; } = "";
    public ComplexityLevel DefaultComplexity { get; init; }
    /// <summary>Recommended built-in preset names (subset of BuiltInPresets.AllNames).</summary>
    public IReadOnlyList<string> RecommendedPresetIds { get; init; } = Array.Empty<string>();
    public PersonaGroup Group { get; init; }
}

/// <summary>Single source of truth for the eight roles.</summary>
public static class PersonaCatalog
{
    private static readonly PersonaDescriptor[] Descriptors =
    {
        new() { Type = PersonaType.Beginner,       NameKey = "PersonaBeginner",       DescriptionKey = "PersonaBeginnerDesc",       IconKey = "PersonaIconBeginner",       DefaultComplexity = ComplexityLevel.Minimal,      RecommendedPresetIds = new[] { "Normal", "Deep Male", "Female" }, Group = PersonaGroup.Entertainment },
        new() { Type = PersonaType.Gamer,          NameKey = "PersonaGamer",          DescriptionKey = "PersonaGamerDesc",          IconKey = "PersonaIconGamer",          DefaultComplexity = ComplexityLevel.Standard,     RecommendedPresetIds = new[] { "Deep Male", "Demon", "Robot" },   Group = PersonaGroup.Entertainment },
        new() { Type = PersonaType.Streamer,       NameKey = "PersonaStreamer",       DescriptionKey = "PersonaStreamerDesc",       IconKey = "PersonaIconStreamer",       DefaultComplexity = ComplexityLevel.Standard,     RecommendedPresetIds = new[] { "Normal", "Robot", "Demon" },        Group = PersonaGroup.ContentCreation },
        new() { Type = PersonaType.Creator,        NameKey = "PersonaCreator",        DescriptionKey = "PersonaCreatorDesc",        IconKey = "PersonaIconCreator",        DefaultComplexity = ComplexityLevel.Advanced,     RecommendedPresetIds = new[] { "Normal", "Deep Male", "Female" }, Group = PersonaGroup.ContentCreation },
        new() { Type = PersonaType.VoiceActor,     NameKey = "PersonaVoiceActor",     DescriptionKey = "PersonaVoiceActorDesc",     IconKey = "PersonaIconVoiceActor",     DefaultComplexity = ComplexityLevel.Professional, RecommendedPresetIds = new[] { "Normal" },                            Group = PersonaGroup.ProfessionalAudio },
        new() { Type = PersonaType.AudioEngineer,  NameKey = "PersonaAudioEngineer",  DescriptionKey = "PersonaAudioEngineerDesc",  IconKey = "PersonaIconAudioEngineer",  DefaultComplexity = ComplexityLevel.Professional, RecommendedPresetIds = new[] { "Normal" },                            Group = PersonaGroup.ProfessionalAudio },
        new() { Type = PersonaType.SocialVoice,    NameKey = "PersonaSocialVoice",    DescriptionKey = "PersonaSocialVoiceDesc",    IconKey = "PersonaIconSocialVoice",    DefaultComplexity = ComplexityLevel.Minimal,      RecommendedPresetIds = new[] { "Robot", "Demon" },                    Group = PersonaGroup.Entertainment },
        new() { Type = PersonaType.AiEnthusiast,   NameKey = "PersonaAiEnthusiast",   DescriptionKey = "PersonaAiEnthusiastDesc",   IconKey = "PersonaIconAiEnthusiast",   DefaultComplexity = ComplexityLevel.Advanced,     RecommendedPresetIds = new[] { "Normal" },                            Group = PersonaGroup.ProfessionalAudio },
    };

    private static readonly Dictionary<PersonaType, PersonaDescriptor> Map =
        Descriptors.ToDictionary(d => d.Type);

    /// <summary>All eight role descriptors, in document order.</summary>
    public static IReadOnlyList<PersonaDescriptor> All { get; } = Descriptors;

    /// <summary>The three broad entry groups used by the onboarding role picker.</summary>
    public static IReadOnlyList<PersonaGroup> Groups { get; } =
        new[] { PersonaGroup.Entertainment, PersonaGroup.ContentCreation, PersonaGroup.ProfessionalAudio };

    public static PersonaDescriptor Get(PersonaType type) => Map[type];

    /// <summary>Roles belonging to a broad group (groups cover all eight exactly once).</summary>
    public static IReadOnlyList<PersonaType> GetGroupMembers(PersonaGroup group) =>
        Descriptors.Where(d => d.Group == group).Select(d => d.Type).ToArray();

    public static bool TryParse(string? value, out PersonaType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Map.ContainsKey(type);

    /// <summary>All persistable identifiers (for state-file validation).</summary>
    public static IReadOnlyList<string> AllIds { get; } =
        Descriptors.Select(d => d.Type.ToString()).ToArray();
}
