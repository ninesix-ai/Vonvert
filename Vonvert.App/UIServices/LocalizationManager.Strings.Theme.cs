// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Theme selector (Settings) ──
    // Label for the palette picker; the theme names themselves are proper nouns and
    // come straight from each palette's displayName (product data, not translated).
    public string ThemeLabel => G();
}
