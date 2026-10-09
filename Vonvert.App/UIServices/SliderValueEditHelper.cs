// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Vonvert.App.UIServices;

/// <summary>
/// Click-to-edit numeric entry for a data-driven DSP slider row. Clicking the value
/// label of a slider opens an inline <see cref="TextBox"/> so a precise value can be
/// typed instead of nudged; the commit parses (culture-invariant), clamps to the
/// slider range and snaps to its tick, then drives the slider so the panel's existing
/// change pipeline runs. The parse/clamp/snap rule is a pure static function so it is
/// unit-testable without a live visual tree.
/// </summary>
public sealed class SliderValueEditHelper
{
    private readonly Slider _slider;
    private readonly TextBlock _label;
    private TextBox? _box;

    private SliderValueEditHelper(TextBlock label, Slider slider)
    {
        _label = label;
        _slider = slider;
    }

    /// <summary>Make <paramref name="label"/> editable against <paramref name="slider"/>.
    /// The edit box is pre-filled with the slider's current raw value.</summary>
    public static SliderValueEditHelper Attach(TextBlock label, Slider slider)
    {
        var h = new SliderValueEditHelper(label, slider);
        label.Cursor = Cursors.Hand;
        label.MouseLeftButtonUp += (_, _) => h.BeginEdit();
        return h;
    }

    private void BeginEdit()
    {
        if (_box != null) return;                 // already editing
        if (_label.Parent is not Grid grid) return;

        var box = new TextBox
        {
            Text = _slider.Value.ToString(CultureInfo.InvariantCulture),
            FontSize = _label.FontSize,
            TextAlignment = TextAlignment.Right,
            Padding = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Match the panel's theme tokens so the editor looks like the label it replaces.
        box.SetResourceReference(Control.BackgroundProperty, "Surface");
        box.SetResourceReference(Control.ForegroundProperty, "TextPrimary");
        box.SetResourceReference(Control.BorderBrushProperty, "Accent");
        Grid.SetColumn(box, Grid.GetColumn(_label));

        _label.Visibility = Visibility.Hidden;
        grid.Children.Add(box);
        _box = box;

        box.Focus();
        box.SelectAll();
        box.KeyDown += OnKeyDown;
        // Commit when focus leaves the box (click elsewhere). A short dispatcher hop lets
        // an Enter keypress commit first, so the two paths never double-run.
        box.LostFocus += (_, _) => box.Dispatcher.BeginInvoke(
            new Action(() => { if (ReferenceEquals(_box, box)) Commit(); }),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; Commit(); }
        else if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
    }

    private void Commit()
    {
        if (_box == null) return;
        string text = _box.Text;
        FinishOverlay();
        double step = _slider.IsSnapToTickEnabled ? _slider.TickFrequency : 0;
        if (TryParseCommit(text, _slider.Minimum, _slider.Maximum, step, out double value))
            _slider.Value = value;   // raises the row's ValueChanged → profile + label update
    }

    private void Cancel() => FinishOverlay();

    private void FinishOverlay()
    {
        if (_box == null) return;
        var box = _box;
        _box = null;
        box.KeyDown -= OnKeyDown;
        if (box.Parent is Grid grid) grid.Children.Remove(box);
        _label.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Pure commit rule: parse <paramref name="text"/> as an invariant-culture number,
    /// clamp to <c>[min, max]</c>, then — when <paramref name="step"/> &gt; 0 — snap to
    /// the nearest tick measured from <paramref name="min"/> (matching WPF's tick
    /// snapping). Returns false when the text has no leading number.
    /// </summary>
    public static bool TryParseCommit(string? text, double min, double max, double step, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            return false;

        parsed = Math.Clamp(parsed, min, max);
        if (step > 0)
        {
            double snapped = Math.Round((parsed - min) / step, MidpointRounding.AwayFromZero) * step + min;
            value = Math.Clamp(snapped, min, max);
        }
        else
        {
            value = parsed;
        }
        return true;
    }
}
