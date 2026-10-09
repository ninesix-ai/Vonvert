// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App;

using Vonvert.App.UIServices;
using Vonvert.Engine.Services;

/// <summary>
/// Global UI font scaling (accessibility). One Settings slider multiplies every font
/// in the window via <see cref="FontScaler"/> (which recomputes from each control's
/// captured design-time size, so it stays idempotent). The chosen scale persists in
/// AppConfig; it is re-applied whenever content is rebuilt (language switch, expert
/// panel refresh) so newly generated controls pick up the current factor.
/// </summary>
public partial class MainWindow
{
    private bool _fontUiReady;
    private double _pendingFontScale = 1.0;
    private DispatcherTimer? _fontSaveTimer;

    /// <summary>Seed the slider from the saved preference and apply it once. Called
    /// from OnServicesInitialized after the window content is live.</summary>
    private void InitFontScale()
    {
        if (FontScaleSlider == null) return;
        double scale = Math.Clamp(AppConfig.Instance.Ui.FontScale, FontScaler.MinScale, FontScaler.MaxScale);
        // Setting Value raises OnFontScaleChanged, but _fontUiReady is still false, so
        // it only updates the label + applies — it never persists the seeded value.
        FontScaleSlider.Value = scale;
        _fontUiReady = true;
    }

    private void OnFontScaleChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateFontScaleText(e.NewValue);
        ApplyFontScale(e.NewValue);
        if (_fontUiReady) ScheduleFontSave(e.NewValue);
    }

    /// <summary>Re-apply the current font scale to the whole window (used after the
    /// visual tree is rebuilt, e.g. a language switch or an expert-panel refresh). The
    /// actual walk is posted to ContextIdle so it runs after the current layout pass,
    /// by which time freshly generated controls are present in the visual tree.</summary>
    private void ReapplyFontScale()
    {
        if (FontScaleSlider == null) return;
        double scale = FontScaleSlider.Value;
        Dispatcher.BeginInvoke(new Action(() => ApplyFontScale(scale)),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ApplyFontScale(double scale)
    {
        try { FontScaler.Apply(RootBorder, scale); }
        catch (Exception ex) { AppLog.Warning(ex, "ApplyFontScale failed"); }
    }

    private void UpdateFontScaleText(double scale)
    {
        if (FontScaleText != null) FontScaleText.Text = $"{(int)Math.Round(scale * 100)}%";
    }

    /// <summary>Debounce persistence so dragging the slider does not thrash the disk.</summary>
    private void ScheduleFontSave(double scale)
    {
        _pendingFontScale = scale;
        _fontSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _fontSaveTimer.Stop();
        _fontSaveTimer.Tick -= FontSaveTick;
        _fontSaveTimer.Tick += FontSaveTick;
        _fontSaveTimer.Start();
    }

    private void FontSaveTick(object? s, EventArgs e)
    {
        _fontSaveTimer?.Stop();
        AppConfig.Instance.Ui.FontScale = _pendingFontScale;
        AppConfig.Instance.Save();
    }
}
