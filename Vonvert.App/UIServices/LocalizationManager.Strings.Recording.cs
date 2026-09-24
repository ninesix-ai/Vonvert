// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Recording view ──
    public string NavRecording       => G();
    public string RecordingTitle     => G();
    public string StartRecording     => G();
    public string StopRecording      => G();
    public string CancelRecording    => G();
    public string RecordMode         => G();
    public string RecordModeProcessed=> G();
    public string RecordModeOriginal => G();
    public string RecordingInProgress=> G();
    public string RecordingSaved     => G();
    public string RecordingHistory   => G();
    public string EmptyRecordingHint => G();
    public string ExportRecording    => G();
    public string DeleteRecording    => G();
    public string ExportSuccess2     => G();
    public string ExportFailed       => G();
    public string SelectAll          => G();
    public string DeleteSelected     => G();
    public string DeleteConfirm      => G();
    public string DeleteConfirmMsg   => G();
    public string DeleteSelectedConfirmMsg => G();
    public string NoItemsSelected    => G();
    public string BatchDone          => G();
    public string RecStatsFormat     => G();

    // ── Recording search / filter ──
    public string RecSearch          => G();
    public string CategoryAll        => G();
    public string RecDateToday       => G();
    public string RecDate7Days       => G();
    public string RecDate30Days      => G();
    public string RecDurUnder1       => G();
    public string RecDur1to5         => G();
    public string RecDur5to15        => G();
    public string RecDurOver15       => G();

    // ── Recording warning dialog ──
    public string RecWarningTitle    => G();
    public string RecWarningBody     => G();

    // ── Export dialog ──
    public string ExportDialogTitle  => G();
    public string ExportFormatLabel  => G();
    public string ExportFormatWav    => G();
    public string ExportFormatMp3    => G();
    public string ExportFormatFlac   => G();
    public string ExportFormatOgg    => G();
    public string ExportFormatAac    => G();
    public string OutputFileLabel    => G();
    public string SampleRateLabel    => G();
    public string BitDepthLabel      => G();
    public string BitDepth16         => G();
    public string BitDepth24         => G();
    public string BitDepth32Float    => G();
    public string ChannelsLabel      => G();
    public string ExportMono         => G();
    public string ExportStereo       => G();
    public string BitrateLabel       => G();
    public string VbrModeLabel       => G();
    public string VbrQualityLabel    => G();
    public string CompressionLabel   => G();
    public string QualityLabel       => G();
    public string AllFiles           => G();
    public string Cancel             => G();
}
