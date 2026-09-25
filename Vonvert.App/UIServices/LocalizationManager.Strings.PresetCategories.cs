// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Preset category display names (group headers + filter chips on the Voices tab) ──
    // CategoryAll is defined in LocalizationManager.Strings.Recording.cs and reused here.
    public string CategoryVoice     => G();
    public string CategoryFx        => G();
    public string CategoryFun       => G();
    public string CategorySpace     => G();
    public string CategoryCharacter => G();
    public string CategoryUser      => G();
    public string CategoryOther     => G();
}
