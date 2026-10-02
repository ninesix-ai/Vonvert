// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.ComponentModel;

namespace Vonvert.App.UIServices;

/// <summary>
/// Shared row view-model for a single soundboard pad, consumed by both the docked
/// Soundboard tab and the floating mini-player so card visuals/state cannot drift between
/// them. Only the mutable bits notify. Promoted from the former private SoundPadVM.
/// </summary>
public sealed class SoundPadItemViewModel : INotifyPropertyChanged
{
    public string Id { get; }
    /// <summary>Built-in English name (or imported file name): the fallback label used
    /// when a language has no localized name for this sound.</summary>
    public string EnglishName { get; }

    private string _name;
    /// <summary>Localized pad label; refreshed on language switch.</summary>
    public string Name { get => _name; set { if (_name != value) { _name = value; OnChanged(nameof(Name)); } } }

    public string Emoji { get; }
    public string Category { get; }
    public bool IsUser { get; }
    public string HotkeyText { get; set; } = "";

    private string _badge = "";
    public string BadgeText { get => _badge; set { if (_badge != value) { _badge = value; OnChanged(nameof(BadgeText)); } } }

    private bool _playing;
    public bool IsPlaying { get => _playing; set { if (_playing != value) { _playing = value; OnChanged(nameof(IsPlaying)); } } }

    private string? _durationText;
    /// <summary>Tiered duration label (50ms / 1.2s / 30s) inlined after the name and mirrored
    /// in the tooltip; null until resolved (a null tooltip stays hidden).</summary>
    public string? DurationText { get => _durationText; set { if (_durationText != value) { _durationText = value; OnChanged(nameof(DurationText)); } } }

    private bool _pinned;
    /// <summary>Whether this pad is pinned to the floating mini-player (drives the 📌 / border highlight).</summary>
    public bool IsPinned { get => _pinned; set { if (_pinned != value) { _pinned = value; OnChanged(nameof(IsPinned)); } } }

    public SoundPadItemViewModel(string id, string name, string emoji, string category, bool isUser)
    {
        Id = id; EnglishName = name; _name = name; Emoji = emoji; Category = category; IsUser = isUser;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
