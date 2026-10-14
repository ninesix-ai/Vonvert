// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Data location (Settings card) ──
    public string DataLocation            => G();
    public string DataLocationHint        => G();
    public string ChangeLocation          => G();
    public string DataLocationChangedRestart => G();
    public string DataLocationInvalid     => G();
    public string MigrateDataPromptFmt    => G();
    public string DataMigrationFailed     => G();

    // ── Diagnostic log export (Settings > Data location) ──
    public string ExportLogs        => G();
    public string ExportLogsHint    => G();
    public string LogsExportOk      => G();
    public string LogsExportEmpty   => G();
    public string LogsExportFailed  => G();

    // ── Portable mode (Settings > Data location) ──
    public string PortableModeNote          => G();
    public string PortableChangeDisabledHint => G();
}
