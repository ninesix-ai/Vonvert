// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vonvert.App.UIServices;
using Vonvert.Engine.Services;

namespace Vonvert.App.OnboardingSteps;

/// <summary>Onboarding persona picker: three broad groups, each listing its roles.
/// Cards show a clear selected highlight (fixing "clicking does nothing") and the
/// choice is raised through <see cref="SelectionChanged"/>. Pure view — no telemetry.</summary>
public partial class PersonaPickStep : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private readonly PersonaSelection _sel = new();
    private readonly Dictionary<PersonaType, Button> _cards = new();

    /// <summary>Raised with the chosen persona whenever the user picks a role card.</summary>
    public event Action<PersonaType?>? SelectionChanged
    {
        add => _sel.Changed += value;
        remove => _sel.Changed -= value;
    }

    public PersonaPickStep()
    {
        InitializeComponent();
        _sel.Changed += _ => ApplyHighlight();
        Build();
    }

    /// <summary>Rebuild text after a language switch (keeps current selection highlight).</summary>
    public void RefreshLocalizedContent() => Build();

    private void Build()
    {
        _cards.Clear();
        Root.Children.Clear();
        foreach (var group in PersonaCatalog.Groups)
        {
            Root.Children.Add(new TextBlock
            {
                Text = L.GetPersonaLabel(GroupKey(group)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 4)
            });
            var wrap = new WrapPanel();
            foreach (var type in PersonaCatalog.GetGroupMembers(group))
            {
                var card = MakeCard(type);
                _cards[type] = card;
                wrap.Children.Add(card);
            }
            Root.Children.Add(wrap);
        }
        ApplyHighlight();
    }

    private Button MakeCard(PersonaType type)
    {
        var d = PersonaCatalog.Get(type);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = L.GetPersonaLabel(d.NameKey), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = L.GetPersonaLabel(d.DescriptionKey), FontSize = 11, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
        var b = new Button
        {
            Content = panel,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 8, 10, 8),
            MinWidth = 160,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = type,
            Focusable = true,
        };
        b.Click += (_, _) => _sel.Select(type);
        return b;
    }

    /// <summary>Repaint every card to reflect the current selection (accent border + tint).</summary>
    private void ApplyHighlight()
    {
        var accent = TryFindResource("Accent") as Brush;
        var tint = new SolidColorBrush(Color.FromArgb(0x33, 0x8B, 0x5C, 0xF6));
        if (accent is not null) tint.Color = Color.FromArgb(0x33,
            ((SolidColorBrush)accent).Color.R, ((SolidColorBrush)accent).Color.G, ((SolidColorBrush)accent).Color.B);

        foreach (var (type, card) in _cards)
        {
            bool on = _sel.IsSelected(type);
            card.BorderBrush = on ? accent : Brushes.Transparent;
            card.BorderThickness = new Thickness(on ? 2 : 0);
            card.Background = on ? tint : Brushes.Transparent;
        }
    }

    private static string GroupKey(PersonaGroup g) => g switch
    {
        PersonaGroup.Entertainment => "PersonaGroupEntertainment",
        PersonaGroup.ContentCreation => "PersonaGroupContentCreation",
        PersonaGroup.ProfessionalAudio => "PersonaGroupProfessionalAudio",
        _ => "PersonaGroupEntertainment"
    };
}
