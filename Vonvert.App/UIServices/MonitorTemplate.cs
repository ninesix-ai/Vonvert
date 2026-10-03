// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;

namespace Vonvert.App.UIServices;

/// <summary>
/// A one-click view for a job. Before these, getting the window into "the view I want while
/// I stream" meant knowing that double-clicking a panel fills it, choosing a size, and doing
/// it again next session.
/// </summary>
public enum MonitorTemplate
{
    /// <summary>All three panels at once, the layout the window shipped with.</summary>
    Diagnose,
    /// <summary>The voice detail fills the window, with the voice name fading in for viewers.</summary>
    Stream,
    /// <summary>The loudness meter fills the window, nothing flashing over it.</summary>
    Loudness,
    /// <summary>Pitch: detail view with the pitch curve given real height for a lesson.</summary>
    Teaching,
}

/// <summary>
/// What a template applies. Deliberately narrow: it does not touch the capture palette or the
/// text size, because those are explicit choices in the same popup and a template that
/// overwrote them would make both controls report a state the screen is not showing.
/// </summary>
public readonly record struct MonitorTemplateLayout(
    MonitorPane? Maximized, bool ShowPresetOverlay, double CurveStripHeight);

/// <summary>Templates, cycling order and copy keys.</summary>
public static class MonitorTemplates
{
    /// <summary>The pitch curve strip as the window shipped it.</summary>
    public const double DefaultCurveStrip = 80;

    /// <summary>Enough taller that a student can follow the contour across a room.</summary>
    public const double TeachingCurveStrip = 150;

    public static MonitorTemplateLayout For(MonitorTemplate template) => template switch
    {
        // On air: the picture everyone looks at, and the voice name they are told.
        MonitorTemplate.Stream => new MonitorTemplateLayout(MonitorPane.Waterfall, true, DefaultCurveStrip),
        // Meter watching: numbers only, so nothing may flash over them.
        MonitorTemplate.Loudness => new MonitorTemplateLayout(MonitorPane.Loudness, false, DefaultCurveStrip),
        // Pitch work: the curve and the note badge live in the detail panel, so fill that one
        // and give the curve the height - this is what separates it from the streaming view.
        MonitorTemplate.Teaching => new MonitorTemplateLayout(MonitorPane.Waterfall, false, TeachingCurveStrip),
        _ => new MonitorTemplateLayout(null, true, DefaultCurveStrip),
    };

    /// <summary>
    /// The order the views are offered in: entry n is picked by the digit n and listed nth in
    /// the appearance popup. It is a single list rather than two, because a re-ordering of the
    /// buttons that did not move the digits would silently rebind every shortcut.
    /// </summary>
    public static readonly MonitorTemplate[] Order =
        { MonitorTemplate.Diagnose, MonitorTemplate.Stream, MonitorTemplate.Loudness, MonitorTemplate.Teaching };

    public static string NameKeyFor(MonitorTemplate template) => template switch
    {
        MonitorTemplate.Stream => "FsTemplateStream",
        MonitorTemplate.Loudness => "FsTemplateLoudness",
        MonitorTemplate.Teaching => "FsTemplateTeaching",
        _ => "FsTemplateDiagnose",
    };

    /// <summary>Heading of the template group inside the appearance popup.</summary>
    public const string SectionKey = "FsTemplates";
}
