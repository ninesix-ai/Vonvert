// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Vonvert.App.UIServices;
using Vonvert.Engine.PresetLibrary;

namespace Vonvert.App.Controls;

/// <summary>
/// Data-driven "expert" parameter panel. The whole UI is generated from
/// <see cref="DspParameterCatalog"/> — one collapsible card per DSP effect, an
/// on/off switch where the effect has one, and a labelled slider (or checkbox
/// for boolean flags) per parameter.
///
/// It edits a working <see cref="VoiceProfile"/> (never a shared built-in), and
/// raises <see cref="ParameterChanged"/> after every edit so the host can push
/// the change to the live engine. <see cref="Load"/> re-points the panel at a
/// different profile (e.g. when a preset is selected) and refreshes all controls.
/// </summary>
public partial class ExpertParamsControl : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;

    private VoiceProfile? _profile;
    private bool _building;
    private bool _showAll;   // false = Simple (essential groups only), true = Professional (all)

    // Actions that pull the current profile value into a control (used on Load).
    private readonly List<Action> _refresh = new();

    // Label TextBlocks + their catalog keys, so a language switch can re-translate
    // them (they are assigned imperatively at build time, not XAML-bound).
    private readonly List<(TextBlock block, string key)> _labels = new();

    // Row elements that carry a hover tooltip + the tooltip's catalog key, tracked
    // separately so a language switch can re-translate the ToolTip text too.
    private readonly List<(FrameworkElement target, string key)> _tips = new();

    /// <summary>Raised after the user changes any parameter or effect toggle.</summary>
    public event Action? ParameterChanged;

    public ExpertParamsControl()
    {
        InitializeComponent();
        BuildGroups();
    }

    /// <summary>Point the panel at a profile and refresh every control to match it.</summary>
    public void Load(VoiceProfile profile)
    {
        _profile = profile;
        foreach (var r in _refresh) r();
    }

    /// <summary>Switch between Simple (essential groups) and Professional (all groups).</summary>
    public void SetMode(bool professional)
    {
        if (_showAll == professional) return;
        _showAll = professional;
        BuildGroups();   // rebuilds + refreshes against the current profile
    }

    private void BuildGroups()
    {
        _building = true;
        try
        {
            Host.Children.Clear();
            _refresh.Clear();
            _labels.Clear();
            _tips.Clear();
            foreach (var group in DspParameterCatalog.Groups.Where(g => _showAll || g.Essential))
                Host.Children.Add(BuildGroup(group));
            // Pull current values into the freshly built controls.
            if (_profile != null)
                foreach (var r in _refresh) r();
        }
        finally { _building = false; }
    }

    /// <summary>Re-translate every label after a UI language change.</summary>
    public void Relabel()
    {
        foreach (var (block, key) in _labels)
            block.Text = L.GetParamLabel(key);
        foreach (var (target, key) in _tips)
            target.ToolTip = L.GetParamLabel(key);
    }

    // Attach a localized hover tooltip to a row element and remember it so a
    // later language switch re-translates it (the panel is built imperatively).
    private void ApplyTip(FrameworkElement target, string tipKey)
    {
        target.ToolTip = L.GetParamLabel(tipKey);
        _tips.Add((target, tipKey));
    }

    private TextBlock MakeLabel(string key, double size, string brush, FontWeight? weight = null)
    {
        var tb = new TextBlock { Text = L.GetParamLabel(key), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        tb.SetResourceReference(ForegroundProperty, brush);
        if (weight.HasValue) tb.FontWeight = weight.Value;
        _labels.Add((tb, key));
        return tb;
    }

    private FrameworkElement BuildGroup(DspGroup group)
    {
        var body = new StackPanel();

        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = body,
        };
        card.SetResourceReference(BackgroundProperty, "Surface");
        card.SetResourceReference(BorderBrushProperty, "Border");
        card.BorderThickness = new Thickness(1);

        // ── Header: name (+ enable switch) ──
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = MakeLabel(group.NameKey, 12.5, "TextPrimary", FontWeights.SemiBold);
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        ToggleButton? enableToggle = null;
        if (group.HasEnable)
        {
            enableToggle = new ToggleButton { VerticalAlignment = VerticalAlignment.Center };
            enableToggle.SetResourceReference(StyleProperty, "MiniToggle");
            enableToggle.Checked += (_, _) => { if (_profile != null && group.SetEnable != null && !_building) { group.SetEnable(_profile, true); ParameterChanged?.Invoke(); } };
            enableToggle.Unchecked += (_, _) => { if (_profile != null && group.SetEnable != null && !_building) { group.SetEnable(_profile, false); ParameterChanged?.Invoke(); } };
            Grid.SetColumn(enableToggle, 1);
            header.Children.Add(enableToggle);
        }
        body.Children.Add(header);

        // ── Param rows ──
        var rows = new StackPanel();
        body.Children.Add(rows);
        foreach (var p in group.Params)
            rows.Children.Add(BuildParamRow(p, group, enableToggle));

        // ── Refresh wiring (pull profile → controls) ──
        if (group.HasEnable && enableToggle != null)
        {
            var tg = enableToggle; var ge = group.GetEnable!;
            _refresh.Add(() => { if (_profile != null) tg.IsChecked = ge(_profile); });
        }

        // Dim the parameter rows when the effect is off (only when it has a switch).
        if (group.HasEnable)
        {
            var ge = group.GetEnable!;
            _refresh.Add(() => { if (_profile != null) rows.Opacity = ge(_profile) ? 1.0 : 0.4; });
        }
        else
        {
            rows.Opacity = 1.0;
        }

        return card;
    }

    private FrameworkElement BuildParamRow(DspParam p, DspGroup group, ToggleButton? enableToggle)
    {
        if (p.IsBool)
            return BuildBoolRow(p);
        return BuildSliderRow(p);
    }

    private FrameworkElement BuildSliderRow(DspParam p)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

        var label = MakeLabel(p.LabelKey, 11.5, "TextSub");
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(label, 0);

        var value = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Margin = new Thickness(8, 0, 0, 0) };
        value.SetResourceReference(ForegroundProperty, "TextSub");
        Grid.SetColumn(value, 2);

        var slider = new Slider
        {
            Minimum = p.Min,
            Maximum = p.Max,
            VerticalAlignment = VerticalAlignment.Center,
            IsMoveToPointEnabled = true,
        };
        if (p.Step > 0) { slider.TickFrequency = p.Step; slider.IsSnapToTickEnabled = true; }
        slider.ValueChanged += (_, e) =>
        {
            if (_building || _profile == null) return;
            p.SetF!(_profile, (float)e.NewValue);
            value.Text = Format(p, (float)e.NewValue);
            ParameterChanged?.Invoke();
        };
        Grid.SetColumn(slider, 1);

        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(value);

        if (p.TipKey != null) { ApplyTip(label, p.TipKey); ApplyTip(slider, p.TipKey); }

        _refresh.Add(() =>
        {
            if (_profile == null) return;
            var v = p.GetF!(_profile);
            slider.Value = v;
            value.Text = Format(p, v);
        });

        return grid;
    }

    private FrameworkElement BuildBoolRow(DspParam p)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = MakeLabel(p.LabelKey, 11.5, "TextSub");
        Grid.SetColumn(label, 0);

        var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        check.Checked += (_, _) => { if (!_building && _profile != null) { p.SetB!(_profile, true); ParameterChanged?.Invoke(); } };
        check.Unchecked += (_, _) => { if (!_building && _profile != null) { p.SetB!(_profile, false); ParameterChanged?.Invoke(); } };
        Grid.SetColumn(check, 1);

        grid.Children.Add(label);
        grid.Children.Add(check);

        if (p.TipKey != null) { ApplyTip(label, p.TipKey); ApplyTip(check, p.TipKey); }

        _refresh.Add(() => { if (_profile != null) check.IsChecked = p.GetB!(_profile); });
        return grid;
    }

    private static string Format(DspParam p, float v)
    {
        // Whole-number-ish magnitudes (frequencies, ms) read better without decimals.
        if (p.LabelKey.EndsWith("Hz", StringComparison.Ordinal) || p.LabelKey.Contains("Time"))
            return ((int)Math.Round(v)).ToString();
        return v.ToString("0.##");
    }
}
