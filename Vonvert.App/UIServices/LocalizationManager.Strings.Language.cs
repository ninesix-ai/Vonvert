// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Language selector (Settings) ──
    // NOTE: not "Language" — that name is taken by the LocalizationManager.Language
    // property (the active language code). The selector label uses LanguageLabel.
    public string LanguageLabel => G();
}
