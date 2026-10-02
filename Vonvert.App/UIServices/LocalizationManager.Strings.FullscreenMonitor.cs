// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.ComponentModel;

namespace Vonvert.App.UIServices;

/// <content>
/// Fullscreen professional monitor labels (header entry tooltip, pane headers,
/// dry/wet taps, loudness meter).
/// </content>
public partial class LocalizationManager
{
    // ── Fullscreen monitor ──
    public string FullscreenViz      => G();
    public string EscToClose         => G();
    public string FsSpectrogram      => G();
    public string FsWaveform         => G();
    public string FsDry              => G();
    public string FsWet              => G();
    public string FsLoudness         => G();
    public string FsTargetLufs       => G();
    public string FsIntegrated       => G();
    public string FsResetIntegrated  => G();

    // ── Chrome toolbar (hover-revealed) and the help card ──
    public string FsClose            => G();
    public string FsTopmost          => G();
    public string FsSize             => G();
    public string FsRestoreLayout    => G();
    public string FsHelpBtn          => G();
    public string FsHelpTitle        => G();
    public string FsHelpDrag         => G();
    public string FsHelpDblClick     => G();
    public string FsHelpRightClick   => G();
    public string FsHelpFocus        => G();
    public string FsLabelsPlain      => G();
    public string FsLabelsTechnical  => G();

    // ── State and failure copy (what the picture is not telling you) ──
    public string FsStateNotRunning  => G();
    public string FsStateNoInput     => G();
    public string FsStateDryBypass   => G();
    public string FsErrOpenFailed    => G();
    public string FsCurrentSource    => G();

    // ── First-run walkthrough (one-time, its own marker) ──
    public string FsTourTitle        => G();

    /// <summary>Link line at the bottom of the help card.</summary>
    public string FsGuideLink        => G();

    // Which waveform the monitor is drawing. Bound instead of assigned so the strip
    // under the taps re-labels itself on a language change, like every other string.
    private bool _monitorShowsDry;

    /// <summary>True while the waveform strip is showing the dry (raw) capture.</summary>
    public bool MonitorShowsDry
    {
        get => _monitorShowsDry;
        set
        {
            if (_monitorShowsDry == value) return;
            _monitorShowsDry = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorShowsDry)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorSourceLabel)));
        }
    }

    /// <summary>"Current: Changed voice" - which signal the picture is really of. The
    /// dry/wet taps are small and their tint difference is subtle, so the source is also
    /// spelled out in words.</summary>
    public string MonitorSourceLabel =>
        string.Format(FsCurrentSource, MonitorShowsDry ? FsDryLabel : FsWetLabel);

    // The monitor window works out which state it is in; the wording lives here so the
    // banner re-translates on a language change like every other label, with no
    // imperative Text assignment to remember.
    private GuidanceKind _monitorGuidance = GuidanceKind.None;

    /// <summary>True when the monitor should show its one-line explanation.</summary>
    public bool MonitorGuidanceVisible => _monitorGuidance != GuidanceKind.None;

    /// <summary>The sentence for the current guidance state, empty when there is none.</summary>
    public string MonitorGuidanceText => GetUiString(MonitorGuidanceModel.KeyFor(_monitorGuidance));

    /// <summary>Called by the monitor on its render tick; ignores no-op updates so a 30 fps
    /// loop does not raise a change notification every frame.</summary>
    public void SetMonitorGuidance(GuidanceKind kind)
    {
        if (_monitorGuidance == kind) return;
        _monitorGuidance = kind;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorGuidanceVisible)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorGuidanceText)));
    }

    // ── Plain-word / technical vocabulary switch ──
    //
    // Exposed as computed bound properties on purpose, the same way PttHoldModeLabel
    // works: LoadLanguage raises PropertyChanged for every string property, so the
    // monitor re-labels itself on a language change with no imperative re-assignment
    // to remember, and ToggleMonitorLabelMode raises the same names on a mode change.

    /// <summary>The monitor's active vocabulary mode. Owned here so every surface that
    /// labels the monitor asks the same object.</summary>
    public MonitorLabels MonitorLabels { get; } = new();

    public string FsSpectrogramLabel => LabelOf(MonitorLabelSlot.Spectrogram);
    public string FsSpectrogramSub   => SubOf (MonitorLabelSlot.Spectrogram);
    public string FsWaveformLabel    => LabelOf(MonitorLabelSlot.Waveform);
    public string FsWaveformSub      => SubOf (MonitorLabelSlot.Waveform);
    public string FsLoudnessLabel    => LabelOf(MonitorLabelSlot.Loudness);
    public string FsLoudnessSub      => SubOf (MonitorLabelSlot.Loudness);
    public string FsDryLabel         => LabelOf(MonitorLabelSlot.Dry);
    public string FsWetLabel         => LabelOf(MonitorLabelSlot.Wet);

    /// <summary>Name of the vocabulary the button would switch TO, so the button always
    /// reads as an offer rather than as a state the user has to decode.</summary>
    public string FsLabelModeLabel =>
        MonitorLabels.Mode == MonitorLabelMode.Plain ? FsLabelsTechnical : FsLabelsPlain;

    public void ToggleMonitorLabelMode()
    {
        MonitorLabels.Toggle();
        NotifyMonitorLabelsChanged();
    }

    /// <summary>Raise the change for every monitor label property after a mode switch.</summary>
    public void NotifyMonitorLabelsChanged()
    {
        foreach (var name in new[]
        {
            nameof(FsSpectrogramLabel), nameof(FsSpectrogramSub),
            nameof(FsWaveformLabel),    nameof(FsWaveformSub),
            nameof(FsLoudnessLabel),    nameof(FsLoudnessSub),
            nameof(FsDryLabel),         nameof(FsWetLabel),
            nameof(FsLabelModeLabel),
        })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private string LabelOf(MonitorLabelSlot slot) => GetUiString(MonitorLabels.KeyFor(slot));

    // An absent sub-label binds as empty text, never as the literal key name.
    private string SubOf(MonitorLabelSlot slot)
        => MonitorLabels.SubKeyFor(slot) is { } key ? GetUiString(key) : string.Empty;
}
