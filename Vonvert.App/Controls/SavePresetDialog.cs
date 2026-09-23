// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Windows;
using System.Windows.Controls;
using Vonvert.App.UIServices;

namespace Vonvert.App.Controls;

/// <summary>
/// Minimal modal "save preset" prompt: a single name field. Returns the trimmed
/// name via <see cref="ChosenName"/> when the user confirms. Built in code (no
/// XAML) and themed via dynamic resources, matching the minimal GitHub build.
/// </summary>
public sealed class SavePresetDialog : Window
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private readonly TextBox _nameBox;

    /// <summary>Non-empty trimmed name entered by the user; empty if cancelled.</summary>
    public string ChosenName { get; private set; } = string.Empty;

    public SavePresetDialog(string initialName = "")
    {
        Title = L.SavePresetTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 340;
        MaxWidth = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        root.Children.Add(new TextBlock
        {
            Text = L.PresetNameLabel,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 6),
        }.Also(tb => tb.SetResourceReference(ForegroundProperty, "TextSub")));

        _nameBox = new TextBox
        {
            Text = initialName,
            FontSize = 13.5,
            Padding = new Thickness(6, 4, 6, 4),
        };
        _nameBox.SetResourceReference(BackgroundProperty, "Surface");
        _nameBox.SetResourceReference(ForegroundProperty, "TextPrimary");
        _nameBox.SetResourceReference(BorderBrushProperty, "Border");
        root.Children.Add(_nameBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };

        var save = new Button { Content = L.SaveBtn, MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        save.SetResourceReference(StyleProperty, "OutlineButton");
        save.Click += (_, _) => OnAccept();

        var cancel = new Button { Content = L.CancelBtn, MinWidth = 88, IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "OutlineButton");
        cancel.Click += (_, _) => { ChosenName = string.Empty; DialogResult = false; Close(); };

        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = new Border
        {
            Padding = new Thickness(0),
            Child = root,
        };
        ((Border)Content).SetResourceReference(BackgroundProperty, "Surface");

        Loaded += (_, _) => { _nameBox.Focus(); _nameBox.SelectAll(); };
    }

    private void OnAccept()
    {
        var name = _nameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;   // keep the dialog open on empty input
        ChosenName = name;
        DialogResult = true;
        Close();
    }
}

/// <summary>Small fluent helper so a resource reference can be applied inline.</summary>
internal static class ControlExtensions
{
    public static T Also<T>(this T obj, System.Action<T> apply) { apply(obj); return obj; }
}
