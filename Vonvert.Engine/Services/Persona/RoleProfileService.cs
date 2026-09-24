// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Vonvert.Engine.Services;

/// <summary>
/// Role state hub. Core formula: EffectiveComplexity = ManualOverride ?? max(active persona defaults).
/// Unconfigured (no personas, no override) resolves to Standard (L2) for backwards compatibility.
/// Manual override wins over personas. Downgrade never destroys parameter values (this service only
/// computes complexity; it never touches VoiceProfile/parameters). All reads/writes are lock-guarded;
/// events fire outside the lock.
/// </summary>
public sealed class RoleProfileService : INotifyPropertyChanged
{
    public static RoleProfileService Instance { get; } = new();

    /// <summary>Isolated instance for tests (avoids shared-singleton cross-talk under parallel runs).</summary>
    internal static RoleProfileService CreateForTests() => new();

    private static readonly PersonaType[] AllPersonaOrder = Enum.GetValues<PersonaType>();

    private readonly object _lock = new();
    private readonly List<PersonaType> _activePersonas = new();
    private ComplexityLevel? _manualOverride;
    private bool _hasCompletedOnboarding;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Selected persona set (document order, de-duplicated; empty = unconfigured).</summary>
    public IReadOnlyList<PersonaType> ActivePersonas { get { lock (_lock) return _activePersonas.ToArray(); } }

    /// <summary>Manual complexity override (null = follow personas).</summary>
    public ComplexityLevel? ManualOverride { get { lock (_lock) return _manualOverride; } set => SetManualOverride(value); }

    /// <summary>In-memory mirror of the onboarding_done marker; not persisted here (single source lives in the file).</summary>
    public bool HasCompletedOnboarding
    {
        get { lock (_lock) return _hasCompletedOnboarding; }
        set { lock (_lock) { if (_hasCompletedOnboarding == value) return; _hasCompletedOnboarding = value; } Raise(nameof(HasCompletedOnboarding)); }
    }

    /// <summary>Manual override wins, otherwise the max default complexity across active personas.</summary>
    public ComplexityLevel EffectiveComplexity
    {
        get
        {
            lock (_lock)
                return _manualOverride
                       ?? (_activePersonas.Count == 0
                           ? ComplexityLevel.Standard
                           : _activePersonas.Max(p => PersonaCatalog.Get(p).DefaultComplexity));
        }
    }

    /// <summary>Whether the user has explicitly configured a role or complexity.</summary>
    public bool IsConfigured { get { lock (_lock) return _activePersonas.Count > 0 || _manualOverride is not null; } }

    public void SetPersonas(IEnumerable<PersonaType> personas)
    {
        bool changed;
        lock (_lock)
        {
            var next = personas.Distinct().OrderBy(p => Array.IndexOf(AllPersonaOrder, p)).ToList();
            changed = !next.SequenceEqual(_activePersonas);
            if (changed) { _activePersonas.Clear(); _activePersonas.AddRange(next); }
        }
        if (changed) RaiseDerived(nameof(ActivePersonas));
    }

    public void AddPersona(PersonaType persona)
    {
        bool changed;
        lock (_lock) { changed = !_activePersonas.Contains(persona); if (changed) _activePersonas.Add(persona); }
        if (changed) RaiseDerived(nameof(ActivePersonas));
    }

    public void RemovePersona(PersonaType persona)
    {
        bool changed;
        lock (_lock) changed = _activePersonas.Remove(persona);
        if (changed) RaiseDerived(nameof(ActivePersonas));
    }

    /// <summary>Set the manual complexity override (downgrade confirmation is a UI concern).</summary>
    public void SetManualOverride(ComplexityLevel? level)
    {
        bool changed;
        lock (_lock) { changed = _manualOverride != level; if (changed) _manualOverride = level; }
        if (changed) RaiseDerived(nameof(ManualOverride));
    }

    /// <summary>Reset to default: clear personas + Minimal override + mark onboarding complete.</summary>
    public void ResetToDefault()
    {
        lock (_lock) { _activePersonas.Clear(); _manualOverride = ComplexityLevel.Minimal; _hasCompletedOnboarding = true; }
        RaiseDerived(nameof(ActivePersonas));
        Raise(nameof(HasCompletedOnboarding));
    }

    /// <summary>Load from a persisted persona section; unknown ids / bad complexity are skipped (forward compatible).</summary>
    public void Load(AppConfig.PersonaSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        lock (_lock)
        {
            _activePersonas.Clear();
            foreach (var id in section.ActivePersonas ?? new List<string>())
                if (PersonaCatalog.TryParse(id, out var p) && !_activePersonas.Contains(p)) _activePersonas.Add(p);
            _manualOverride = ParseComplexity(section.ComplexityLevelOverride);
        }
        RaiseDerived(nameof(ActivePersonas));
    }

    /// <summary>Write into a persona section (caller persists to disk).</summary>
    public void Save(AppConfig.PersonaSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        lock (_lock)
        {
            section.ActivePersonas = _activePersonas.Select(p => p.ToString()).ToList();
            section.ComplexityLevelOverride = _manualOverride?.ToString();
        }
    }

    private static ComplexityLevel? ParseComplexity(string? value)
        => Enum.TryParse<ComplexityLevel>(value, ignoreCase: true, out var lvl) ? lvl : null;

    private void RaiseDerived(string primary) { Raise(primary); Raise(nameof(EffectiveComplexity)); }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
