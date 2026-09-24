// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Vonvert.App.UIServices;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Settings "My role" card: change the active persona and the complexity override.
/// A downgrade asks for confirmation; tuned parameters are never touched. Hidden entirely
/// when the role system is disabled. Emits no telemetry and shows no upgrade tips.
/// </summary>
public partial class SettingsMyPersonaView : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;
    private static RoleProfileService Role => RoleProfileService.Instance;
    private bool _suppress;

    /// <summary>Raised after the user changes role/complexity so the host can persist + re-apply.</summary>
    public event Action? Changed;

    public SettingsMyPersonaView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshLocalizedContent();
    }

    /// <summary>Rebuild labels + items and reflect current state; call on load and language switch.</summary>
    public void RefreshLocalizedContent()
    {
        _suppress = true;
        try
        {
            Card.Visibility = AppConfig.Instance.Ui.RoleSystemEnabled ? Visibility.Visible : Visibility.Collapsed;
            Title.Text = L.SettingsMyPersonaTitle;
            RoleLabel.Text = L.SettingsMyPersonaRole;
            ComplexityLabel.Text = L.SettingsMyPersonaComplexity;

            RoleCombo.Items.Clear();
            RoleCombo.Items.Add(new ComboBoxItem { Content = L.GetPersonaLabel("PersonaNone"), Tag = (PersonaType?)null });
            foreach (var d in PersonaCatalog.All)
                RoleCombo.Items.Add(new ComboBoxItem { Content = L.GetPersonaLabel(d.NameKey), Tag = (PersonaType?)d.Type });

            ComplexityCombo.Items.Clear();
            foreach (ComplexityLevel lvl in Enum.GetValues<ComplexityLevel>())
                ComplexityCombo.Items.Add(new ComboBoxItem { Content = L.GetPersonaLabel(ComplexityKey(lvl)), Tag = (ComplexityLevel?)lvl });

            var active = Role.ActivePersonas;
            PersonaType? cur = active.Count > 0 ? active[active.Count - 1] : null;
            for (int i = 0; i < RoleCombo.Items.Count; i++)
                if (Equals(((ComboBoxItem)RoleCombo.Items[i]).Tag, cur)) { RoleCombo.SelectedIndex = i; break; }
            for (int i = 0; i < ComplexityCombo.Items.Count; i++)
                if (Equals(((ComboBoxItem)ComplexityCombo.Items[i]).Tag, Role.ManualOverride)) { ComplexityCombo.SelectedIndex = i; break; }
        }
        finally { _suppress = false; }
    }

    private void RoleCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (RoleCombo.SelectedItem is not ComboBoxItem item) return;
        PersonaType? p = item.Tag as PersonaType?;
        Role.SetPersonas(p is null ? Array.Empty<PersonaType>() : new[] { p.Value });
        Changed?.Invoke();
    }

    private void ComplexityCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (ComplexityCombo.SelectedItem is not ComboBoxItem citem) return;
        ComplexityLevel? target = citem.Tag as ComplexityLevel?;
        if (IsDowngrade(target) &&
            MessageBox.Show(L.SettingsMyPersonaDowngradeConfirm, L.SettingsMyPersonaTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            RefreshLocalizedContent();   // revert combo to current value
            return;
        }
        Role.SetManualOverride(target);
        Changed?.Invoke();
    }

    private bool IsDowngrade(ComplexityLevel? target)
        => target is not null && target < Role.EffectiveComplexity;

    private static string ComplexityKey(ComplexityLevel lvl) => lvl switch
    {
        ComplexityLevel.Minimal => "ComplexityMinimal",
        ComplexityLevel.Standard => "ComplexityStandard",
        ComplexityLevel.Advanced => "ComplexityAdvanced",
        _ => "ComplexityProfessional"
    };
}
