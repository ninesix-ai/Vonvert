// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.ComponentModel;
using System.Linq;
using Vonvert.Engine.Services;

namespace Vonvert.App;

/// <summary>
/// Role-system wiring: applies the effective complexity to the expert panel
/// (L1/L2 -> Simple, L3/L4 -> Professional) and pre-selects a recommended built-in
/// preset. The left navigation rail is intentionally unaffected (this build has 3 tabs).
/// No gating, telemetry or tips.
/// </summary>
public partial class MainWindow
{
    private static RoleProfileService Role => RoleProfileService.Instance;

    /// <summary>Load persisted role state at startup (called after InitializeComponent).</summary>
    private void LoadRoleState()
    {
        try
        {
            if (!AppConfig.Instance.Ui.RoleSystemEnabled) return;
            Role.Load(AppConfig.Instance.Persona);
        }
        catch (Exception ex) { AppLog.Warning(ex, "LoadRoleState failed"); }
    }

    private void PersistRoleState()
    {
        try
        {
            Role.Save(AppConfig.Instance.Persona);
            AppConfig.Instance.Save();
        }
        catch (Exception ex) { AppLog.Warning(ex, "PersistRoleState failed"); }
    }

    /// <summary>Called once after services are ready: subscribe + apply current state to the panel.</summary>
    private void InitRoleSystem()
    {
        Role.PropertyChanged += Role_PropertyChanged;
        ApplyRoleToPanel();
    }

    private void Role_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoleProfileService.EffectiveComplexity)
            or nameof(RoleProfileService.ActivePersonas))
            Dispatcher.Invoke(ApplyRoleToPanel);
    }

    /// <summary>Expert panel two-state mode is the complexity landing point: L3/L4 -> Professional.</summary>
    private void ApplyRoleToPanel()
    {
        bool professional = AppConfig.Instance.Ui.RoleSystemEnabled
            && Role.EffectiveComplexity >= ComplexityLevel.Advanced;
        try { ExpertPanel?.SetMode(professional); }
        catch (Exception ex) { AppLog.Warning(ex, "ApplyRoleToPanel failed"); }
    }

    /// <summary>Pre-select the active role's first available built-in recommended preset and persist.
    /// No-op when the role system is off or no persona is configured. Does not touch parameters.</summary>
    private void ApplyRecommendedPresetForActiveRole()
    {
        if (!AppConfig.Instance.Ui.RoleSystemEnabled) return;
        var active = Role.ActivePersonas;
        if (active.Count == 0) return;
        try
        {
            var persona = active[active.Count - 1];
            var rec = PersonaCatalog.Get(persona).RecommendedPresetIds
                .FirstOrDefault(n => App.Presets != null
                    && App.Presets.Presets.Any(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)));
            if (rec != null && MainPresetPicker != null)
            {
                _suppressParamExpand = true;   // apply silently; do not slide the rail open
                try { MainPresetPicker.SetPresetByName(L.GetPresetDisplayName(rec)); }
                finally { _suppressParamExpand = false; }
            }
        }
        catch (Exception ex) { AppLog.Warning(ex, "ApplyRecommendedPreset failed"); }
        finally { PersistRoleState(); }
    }
}
