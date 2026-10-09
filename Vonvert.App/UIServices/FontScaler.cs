// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Vonvert.Engine.Services;

namespace Vonvert.App.UIServices;

/// <summary>
/// Scales every font size in a visual subtree by a single factor while keeping the
/// relative hierarchy of the design-time sizes intact.
/// </summary>
/// <remarks>
/// The previous drift-prone design recorded each element's "base size" as whatever
/// FontSize evaluated to the first time it was visited; for an element that merely
/// inherits its size that value was already the *scaled* parent size, so nesting
/// compounded the error. This version records the design-time size the first time it
/// scales an element and always recomputes from that recorded base, so repeated
/// applications are idempotent. Elements with no size of their own (pure inheritance)
/// are left untouched — they follow their nearest scaled ancestor automatically.
/// </remarks>
public static class FontScaler
{
    /// <summary>Supported font scale range. 1.0 = 100 % (default).</summary>
    public const double MinScale = 1.0;
    public const double MaxScale = 1.40;

    /// <summary>The default (100 %) scale — slider default and reset target.</summary>
    public const double DefaultScale = 1.0;

    private sealed class Holder { public double Value; public Holder(double v) => Value = v; }

    // Design-time (unscaled) size for elements whose local FontSize we own. Presence
    // here means "this local value was written by Apply(), not by XAML", which keeps
    // repeated applications idempotent.
    private static readonly ConditionalWeakTable<DependencyObject, Holder> BaseSizes = new();

    // Popups whose Opened event we already hooked (ComboBox dropdowns etc.).
    private static readonly ConditionalWeakTable<Popup, Holder> HookedPopups = new();

    private static double _lastScale = DefaultScale;

    /// <summary>
    /// Pure size rule (unit-testable, free of WPF and config): design sizes at or
    /// below <paramref name="threshold"/> take an automatic <paramref name="boost"/>
    /// so small text stays readable without enlarging headings; everything is then
    /// multiplied by <paramref name="scale"/> and raised to the <paramref name="floor"/>.
    /// </summary>
    public static double ComputeTarget(double designSize, double scale, double threshold, double boost, double floor)
    {
        double effective = designSize <= threshold ? designSize * (1.0 + boost) : designSize;
        return Math.Max(floor, effective * scale);
    }

    /// <summary>Apply a global font scale to the visual subtree rooted at <paramref name="root"/>.</summary>
    public static void Apply(DependencyObject root, double scale)
    {
        if (root == null) return;
        scale = Math.Clamp(scale, MinScale, MaxScale);
        _lastScale = scale;
        Walk(root, scale);
    }

    private static void Walk(DependencyObject node, double scale)
    {
        if (node is FrameworkElement || node is TextElement)
            ScaleNode(node, scale);

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child != null) Walk(child, scale);
        }

        // Popups (ComboBox dropdowns) live in a separate visual tree the normal child
        // enumeration never reaches. Walk the popup content now if it exists, and hook
        // Opened once so lazily-generated item containers are scaled on first open.
        if (node is Popup popup)
        {
            if (popup.Child != null)
                Walk(popup.Child, scale);

            if (!HookedPopups.TryGetValue(popup, out _))
            {
                HookedPopups.Add(popup, new Holder(0));
                popup.Opened += (_, _) =>
                {
                    if (popup.Child != null) Walk(popup.Child, _lastScale);
                };
            }
        }
    }

    private static void ScaleNode(DependencyObject node, double scale)
    {
        double current = (double)node.GetValue(TextElement.FontSizeProperty);
        if (double.IsNaN(current) || current <= 0.1) return;

        // Already scaled by a previous Apply → reuse the captured design-time size.
        if (BaseSizes.TryGetValue(node, out var recorded))
        {
            WriteScaled(node, recorded.Value, scale, current);
            return;
        }

        bool hasLocal = node.ReadLocalValue(TextElement.FontSizeProperty) != DependencyProperty.UnsetValue;

        if (hasLocal)
        {
            // A real Binding tracks its source automatically — writing over it would
            // freeze and double-scale the value.
            if (node is FrameworkElement feBound &&
                feBound.GetBindingExpression(TextElement.FontSizeProperty) != null)
                return;

            // TemplateBinding evaluates to a plain local value with no detectable
            // expression. If a template child's size equals its templated parent's,
            // assume it tracks the parent via TemplateBinding and leave it alone —
            // the parent is (or will be) scaled itself, and the binding propagates.
            if (node is FrameworkElement feTpl && feTpl.TemplatedParent is FrameworkElement tplParent)
            {
                double parentFs = (double)tplParent.GetValue(TextElement.FontSizeProperty);
                if (Math.Abs(current - parentFs) < 0.001)
                    return;
            }

            // Genuine design-time local value from XAML or code-behind.
            WriteScaled(node, current, scale, current);
            return;
        }

        // No local value: either pure inheritance (leave alone — it already follows the
        // nearest scaled ancestor) or a Style setter (a fixed design-time number that no
        // ancestor will scale for us).
        if (StyleSetsFontSize(node))
            WriteScaled(node, current, scale, current);
    }

    private static void WriteScaled(DependencyObject node, double designSize, double scale, double current)
    {
        if (double.IsNaN(designSize) || designSize <= 0.1) return;

        var ui = AppConfig.Instance.Ui;
        double target = ComputeTarget(designSize, scale, ui.SmallFontThreshold, ui.SmallFontBoost, ui.FontFloorSize);

        if (!BaseSizes.TryGetValue(node, out _))
            BaseSizes.Add(node, new Holder(designSize));
        if (Math.Abs(current - target) > 0.001)
            node.SetValue(TextElement.FontSizeProperty, target);
    }

    /// <summary>True if the element's applied style (or its BasedOn chain) sets FontSize.</summary>
    private static bool StyleSetsFontSize(DependencyObject node)
    {
        if (node is not FrameworkElement fe) return false;
        for (var style = fe.Style; style != null; style = style.BasedOn)
            foreach (var setter in style.Setters)
                if (setter is Setter s && s.Property == TextElement.FontSizeProperty)
                    return true;
        return false;
    }
}
