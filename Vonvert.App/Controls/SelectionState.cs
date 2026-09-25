// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows;

namespace Vonvert.App.Controls;

/// <summary>
/// Attached state used by tile templates to render the unified
/// "selected" visual (gradient border + check badge) declaratively.
/// </summary>
public static class SelectionState
{
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected", typeof(bool), typeof(SelectionState),
            new PropertyMetadata(false));

    public static bool GetIsSelected(DependencyObject d) =>
        (bool)d.GetValue(IsSelectedProperty);

    public static void SetIsSelected(DependencyObject d, bool value) =>
        d.SetValue(IsSelectedProperty, value);
}
