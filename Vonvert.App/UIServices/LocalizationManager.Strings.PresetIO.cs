// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Preset import / export / delete (Voices tab toolbar + tile context menu) ──
    public string ImportPreset          => G();
    public string ExportPreset          => G();
    public string DeletePreset          => G();
    public string PresetImportedFmt     => G();
    public string PresetExportedFmt     => G();
    public string PresetDeletedFmt      => G();
    public string PresetImportFailed    => G();
    public string PresetExportFailed    => G();
    public string DeletePresetConfirmFmt => G();
    public string PresetFileFilter      => G();
    public string AllFilesLabel         => G();
}
