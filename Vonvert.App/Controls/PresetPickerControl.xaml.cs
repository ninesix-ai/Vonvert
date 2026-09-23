// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vonvert.Engine.PresetLibrary;
using Vonvert.App.UIServices;

namespace Vonvert.App.Controls;

/// <summary>
/// Minimal preset picker: a tile grid of built-in (non-user) presets only.
/// Search, category/tag chips, sorting and preview playback are not included.
/// </summary>
public partial class PresetPickerControl : UserControl
{
    private static LocalizationManager L => LocalizationManager.Instance;

    // ── Category → accent color mapping ──
    private static readonly Dictionary<string, Color> s_categoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Voice"]      = Color.FromRgb(0x5B, 0x6B, 0xFF),
        ["FX"]         = Color.FromRgb(0xFF, 0xAA, 0x55),
        ["Fun"]        = Color.FromRgb(0x2E, 0xE8, 0xA0),
        ["Space"]      = Color.FromRgb(0x7C, 0x3A, 0xED),
        ["Character"]  = Color.FromRgb(0xFF, 0x55, 0x77),
    };

    // ── State ──
    private bool _tilesBuilt;
    private Button? _selectedTile;
    private string? _selectedPresetName;
    private bool _userOverrode;
    private Button? _hoveredTile;
    private DispatcherTimer? _hoverTimer;

    // ══════════════════════════════════════════════════════════════
    //  Public API
    // ══════════════════════════════════════════════════════════════

    /// <summary>Currently selected voice profile, or null.</summary>
    public VoiceProfile? SelectedPreset { get; private set; }

    /// <summary>Fired when the user selects a different preset tile.</summary>
    public event Action<VoiceProfile>? PresetChanged;

    /// <summary>Fired on hover after 500 ms — parent can play audio preview.</summary>
    public event Action<VoiceProfile>? PresetHoverPreview;

    /// <summary>Fired when the user clicks Import — the window owns the file dialog
    /// (it has clipboard / toast / App access) and calls back via <see cref="FinishImport"/>.</summary>
    public event Action? ImportRequested;

    /// <summary>Fired when the user clicks Export, with the currently selected preset
    /// (null when nothing is selected). The window shows the save dialog.</summary>
    public event Action<VoiceProfile?>? ExportRequested;

    /// <summary>Fired from a user preset tile's right-click → Delete context menu.</summary>
    public event Action<VoiceProfile>? DeleteRequested;

    public PresetPickerControl()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // App.Presets is initialized asynchronously after the window is shown,
            // so Loaded may fire before it is ready.  Defer gracefully instead of crashing.
            if (App.Presets == null)
                return;
            if (!_tilesBuilt)
                BuildPresetButtons();
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  Public Methods
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Select a preset by its English name.
    /// Respects the smart-follow flag (<see cref="ResetFollowState"/>).
    /// </summary>
    public void SetPresetByName(string name)
    {
        if (_userOverrode) return;
        SelectTile(name, fireChanged: true);
    }

    /// <summary>Clear the current selection (used by Reset Defaults).</summary>
    public void ClearSelection()
    {
        ResetAllTiles();
        _selectedTile = null;
        _selectedPresetName = null;
        SelectedPreset = null;
    }

    /// <summary>
    /// Reset the smart-follow flag so the next
    /// <see cref="SetPresetByName"/> call is honoured again.
    /// </summary>
    public void ResetFollowState()
    {
        _userOverrode = false;
    }

    /// <summary>Rebuild the tile grid.</summary>
    public void RefreshTiles()
    {
        if (App.Presets == null) return;
        BuildPresetButtons();
    }

    /// <summary>
    /// Advance the selection to the next visible preset (wrapping around) and
    /// fire <see cref="PresetChanged"/>. Used by the "Next Preset" global hotkey.
    /// A no-op when there is nothing to cycle through.
    /// </summary>
    public void CycleToNext()
    {
        if (App.Presets == null) return;
        var presets = GetVisiblePresets().ToList();
        if (presets.Count <= 1) return;

        var current = SelectedPreset;
        int idx = current == null ? -1 : presets.FindIndex(p => p.Name == current.Name);
        var next = presets[(idx + 1) % presets.Count];

        // A hotkey-driven cycle is a deliberate user choice — stop auto-following.
        _userOverrode = true;
        SelectTile(L.GetPresetDisplayName(next.Name), fireChanged: true);
    }

    /// <summary>
    /// Rebuild tiles (after the library changed) and make <paramref name="displayName"/>
    /// the active, applied selection. Used to highlight a freshly imported preset.
    /// </summary>
    public void SelectPresetByName(string displayName)
    {
        RefreshTiles();
        _userOverrode = true;
        SelectTile(displayName, fireChanged: true);
    }

    private void ImportBtn_Click(object s, RoutedEventArgs e) => ImportRequested?.Invoke();

    private void ExportBtn_Click(object s, RoutedEventArgs e) => ExportRequested?.Invoke(SelectedPreset);

    // ══════════════════════════════════════════════════════════════
    //  Preset Tile Grid
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Built-in pack voices first (alphabetical), then any user / imported presets
    /// the library has (alphabetical). A preset is "user-owned" when its name is not
    /// a known built-in — those appear as removable tiles. This lets pros bring in
    /// shared .vopreset files and see them alongside the stock voices.
    /// </summary>
    private IEnumerable<VoiceProfile> GetVisiblePresets()
    {
        var all = App.Presets.Presets;
        var builtIn = all.Where(p => BuiltInPresets.AllNames.Contains(p.Name)).OrderBy(p => p.Name);
        var user    = all.Where(p => !BuiltInPresets.AllNames.Contains(p.Name)).OrderBy(p => p.Name);
        return builtIn.Concat(user);
    }

    /// <summary>A preset is deletable only when it is not a built-in pack voice.</summary>
    private static bool IsUserPreset(VoiceProfile p) => !BuiltInPresets.AllNames.Contains(p.Name);

    private void BuildPresetButtons()
    {
        if (App.Presets?.Presets == null) return;

        // Preserve current selection across rebuilds
        var prevName = _selectedPresetName;
        _tilesBuilt = true;

        // Unsubscribe events from old buttons (prevent memory leak)
        UnsubscribeTileEvents();
        TilePanel.Children.Clear();

        foreach (var preset in GetVisiblePresets())
        {
            var btn = new Button
            {
                Content   = preset.Icon,
                Tag       = L.GetPresetDisplayName(preset.Name),
                Style     = FindResource("PresetTile") as Style,
            };
            btn.SetValue(ToolTipService.ToolTipProperty, L.GetPresetDisplayName(preset.Name));
            btn.BorderBrush = GetCategoryBorderBrush(preset.Name);

            btn.Click += PresetTile_Click;
            btn.MouseEnter += PresetTile_MouseEnter;
            btn.MouseLeave += PresetTile_MouseLeave;

            // User presets are removable; built-in pack voices are read-only.
            if (IsUserPreset(preset))
            {
                var menu = new ContextMenu();
                var delete = new MenuItem { Header = L.DeletePreset };
                delete.Click += (_, _) => DeleteRequested?.Invoke(preset);
                menu.Items.Add(delete);
                btn.ContextMenu = menu;
            }

            TilePanel.Children.Add(btn);
        }

        // Restore previously selected preset highlight
        if (prevName != null)
            SelectTile(prevName, fireChanged: false);
    }

    private void UnsubscribeTileEvents()
    {
        foreach (var child in TilePanel.Children)
        {
            if (child is Button btn)
            {
                btn.Click -= PresetTile_Click;
                btn.MouseEnter -= PresetTile_MouseEnter;
                btn.MouseLeave -= PresetTile_MouseLeave;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Tile Click / Selection
    // ══════════════════════════════════════════════════════════════

    private void PresetTile_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button tile || tile.Tag is not string displayName) return;

        var preset = App.Presets.Presets.FirstOrDefault(p => L.GetPresetDisplayName(p.Name) == displayName);
        if (preset == null) return;

        _userOverrode = true;
        SelectTileInternal(tile, preset, displayName);
        PresetChanged?.Invoke(preset);
    }

    /// <summary>
    /// Select a tile by display name — updates visual state and optionally fires PresetChanged.
    /// </summary>
    private void SelectTile(string displayName, bool fireChanged)
    {
        var preset = App.Presets.Presets.FirstOrDefault(p => L.GetPresetDisplayName(p.Name) == displayName);
        if (preset == null) return;

        foreach (var child in TilePanel.Children)
        {
            if (child is not Button btn) continue;
            if (btn.Tag as string == displayName)
            {
                ResetAllTiles();
                HighlightTile(btn);
                _selectedPresetName = displayName;
                SelectedPreset = preset;
                if (fireChanged) PresetChanged?.Invoke(preset);
                return;
            }
        }
    }

    private void SelectTileInternal(Button tile, VoiceProfile preset, string displayName)
    {
        ResetAllTiles();
        HighlightTile(tile);
        _selectedTile = tile;
        _selectedPresetName = displayName;
        SelectedPreset = preset;
    }

    private void ResetAllTiles()
    {
        foreach (var child in TilePanel.Children)
        {
            if (child is not Button b || b.Tag is not string tileName) continue;
            var tilePreset = App.Presets.Presets.FirstOrDefault(p => L.GetPresetDisplayName(p.Name) == tileName);
            if (tilePreset != null) b.BorderBrush = GetCategoryBorderBrush(tilePreset.Name);
            else b.SetResourceReference(Border.BorderBrushProperty, "Border");
            b.SetResourceReference(Button.BackgroundProperty, "TileSurface");
        }
    }

    private void HighlightTile(Button tile)
    {
        tile.SetResourceReference(Button.BorderBrushProperty, "Accent");
        tile.SetResourceReference(Button.BackgroundProperty, "TileSelected");
        _selectedTile = tile;
    }

    // ══════════════════════════════════════════════════════════════
    //  Hover Preview
    // ══════════════════════════════════════════════════════════════

    private void PresetTile_MouseEnter(object s, MouseEventArgs e)
    {
        if (s is not Button tile || tile.Tag is not string displayName) return;
        _hoveredTile = tile;
        if (_hoverTimer == null)
        {
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _hoverTimer.Tick += OnHoverPreviewTick;
        }
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnHoverPreviewTick(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        if (_hoveredTile?.Tag is not string displayName) return;
        var preset = App.Presets.Presets.FirstOrDefault(p => L.GetPresetDisplayName(p.Name) == displayName);
        if (preset != null)
            PresetHoverPreview?.Invoke(preset);
    }

    private void PresetTile_MouseLeave(object s, MouseEventArgs e)
    {
        _hoveredTile = null;
        _hoverTimer?.Stop();
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════

    private Brush GetCategoryBorderBrush(string presetName)
    {
        var category = App.Presets.Index.GetMetadata(presetName)?.Category;
        if (category != null && s_categoryColors.TryGetValue(category, out var color))
        {
            var tinted = Color.FromArgb(0x66, color.R, color.G, color.B);
            return new SolidColorBrush(tinted);
        }
        return FindResource("Border") as Brush ?? Brushes.Transparent;
    }
}
