// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Newtonsoft.Json;

namespace Vonvert.Engine.PresetLibrary;

public record PresetMetadata
{
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Category { get; set; } = "User";
    public bool IsFavorite { get; set; } = false;
    public DateTime LastUsed { get; set; } = DateTime.MinValue;
    public int UseCount { get; set; } = 0;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Modified { get; set; } = DateTime.UtcNow;
}
