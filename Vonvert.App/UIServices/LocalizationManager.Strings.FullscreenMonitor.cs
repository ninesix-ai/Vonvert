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
    /// <summary>Informational: A/B is on DRY, so the meters follow the raw voice.</summary>
    public string FsStateRawVoice    => G();
    public string FsErrOpenFailed    => G();
    public string FsCurrentSource    => G();

    // ── First-run walkthrough (one-time, its own marker) ──
    public string FsTourTitle        => G();

    /// <summary>Link line at the bottom of the help card.</summary>
    public string FsGuideLink        => G();

    // ── Overlay axis on the waterfall (hertz itself needs no copy; it is a symbol) ──
    /// <summary>Sits on the shaded band where voice fundamentals and clarity live.</summary>
    public string FsBandVoice        => G();

    /// <summary>Says which way time runs, because the newest frame is at the right edge.</summary>
    public string FsAxisTimeHint     => G();

    // ── Discoverability: the hidden gestures get visible, clickable hints ──
    public string FsDblClickHint     => G();
    public string FsDblClickRestore  => G();
    public string FsExpandHint       => G();
    public string FsGestureHint      => G();

    // Which panel currently fills the window, so a chip can say "fill" or "restore".
    // Kept next to the other monitor state: the chips are bound, so they re-translate
    // with the UI language without anyone re-assigning Text.
    private MonitorPane? _hintMaximized;

    public string FsWaterfallHint => HintFor(MonitorPane.Waterfall);
    public string FsLoudnessHint  => HintFor(MonitorPane.Loudness);
    public string FsWaveformHint  => HintFor(MonitorPane.Waveform);

    private string HintFor(MonitorPane pane) =>
        GetUiString(_hintMaximized == pane ? "FsDblClickRestore" : "FsDblClickHint");

    public void SetMonitorMaximizedPane(MonitorPane? pane)
    {
        if (_hintMaximized == pane) return;
        _hintMaximized = pane;
        NotifyHintLabelsChanged();
    }

    public void NotifyHintLabelsChanged()
    {
        foreach (var name in new[] { nameof(FsWaterfallHint), nameof(FsLoudnessHint), nameof(FsWaveformHint) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Loudness meter legend, switchable target, peak alert ──
    public string FsLegendMomentary  => G();
    public string FsLegendShort      => G();
    public string FsLegendIntegrated => G();
    public string FsScaleTip         => G();
    public string FsTargetBroadcast  => G();
    public string FsTargetStreaming  => G();
    public string FsTargetPodcast    => G();
    public string FsTargetNone       => G();
    public string FsTpWarning        => G();
    public string FsTpClip           => G();
    public string FsPitchOn          => G();
    public string FsPitchNear        => G();
    public string FsPitchOff         => G();

    // ── Keyboard access and screen-reader names (Acc* prefix, as the rest of the app) ──
    /// <summary>Shortcut line shown on the help card.</summary>
    public string FsHelpKeys         => G();
    public string AccFsWaterfall      => G();
    public string AccFsLoudness       => G();
    public string AccFsWaveform       => G();
    public string AccFsClose          => G();
    public string AccFsTopmost        => G();
    public string AccFsSize           => G();
    public string AccFsResetView      => G();
    public string AccFsLabelMode      => G();
    public string AccFsHelp           => G();
    public string AccFsDry            => G();
    public string AccFsWet            => G();
    public string AccFsTarget         => G();
    public string AccFsResetIntegrated => G();

    /// <summary>
    /// Which delivery target the meter draws its line for. Owned here for the same reason
    /// the guidance banner is: the button label is then a bound string that follows the UI
    /// language, instead of text someone remembers to refresh.
    /// </summary>
    public LufsTargetModel LufsTarget { get; } = new();

    /// <summary>Name of the current target, also the label of the button that changes it.</summary>
    public string MonitorTargetLabel => GetUiString(LufsTarget.LabelKey);

    public void CycleMonitorLufsTarget()
    {
        LufsTarget.Cycle();
        NotifyMonitorTargetChanged();
    }

    /// <summary>Applies a target read back from disk, then refreshes the button label.</summary>
    public void SetMonitorLufsTarget(LufsTargetPreset preset)
    {
        LufsTarget.Set(preset);
        NotifyMonitorTargetChanged();
    }

    public void NotifyMonitorTargetChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorTargetLabel)));

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

    /// <summary>Applies a vocabulary mode read back from disk.</summary>
    public void SetMonitorLabelMode(MonitorLabelMode mode)
    {
        MonitorLabels.SetMode(mode);
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

    // ── capture colour profiles ──
    public string FsColorScheme       => G();   // carries {0}
    public string FsColorBrand        => G();
    public string FsColorHighContrast => G();
    public string FsColorChroma       => G();
    public string FsColorNeutral      => G();

    /// <summary>
    /// Which surface set the monitor is painting. Owned here like the other monitor choices
    /// so the button, the about surface and any future page ask the same object; also makes
    /// the label a binding, which refreshes with the UI language on its own.
    /// </summary>
    public MonitorVisualProfile MonitorProfile { get; private set; } = MonitorVisualProfile.Brand;

    public string MonitorProfileName => GetUiString(MonitorVisualPalettes.NameKeyFor(MonitorProfile));

    /// <summary>Button text: the offer and the current answer together, so a glance says what
    /// will happen and what is on screen now.</summary>
    public string MonitorProfileButton => string.Format(FsColorScheme, MonitorProfileName);

    /// <summary>Applies a profile read back from disk.</summary>
    public void SetMonitorProfile(MonitorVisualProfile profile)
    {
        if (MonitorProfile == profile) return;
        MonitorProfile = profile;
        NotifyMonitorProfileChanged();
    }

    /// <summary>Advance to the next profile and report what it is, for the caller that has to
    /// repaint straight away.</summary>
    public MonitorVisualProfile CycleMonitorProfile()
    {
        var next = MonitorVisualPalettes.Next(MonitorProfile);
        MonitorProfile = next;
        NotifyMonitorProfileChanged();
        return next;
    }

    public void NotifyMonitorProfileChanged()
    {
        foreach (var name in new[] { nameof(MonitorProfileName), nameof(MonitorProfileButton) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // An absent sub-label binds as empty text, never as the literal key name.
    private string SubOf(MonitorLabelSlot slot)
        => MonitorLabels.SubKeyFor(slot) is { } key ? GetUiString(key) : string.Empty;
}
