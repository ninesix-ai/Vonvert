// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vonvert.App.UIServices;
using Vonvert.Engine;

namespace Vonvert.App.Controls;

/// <summary>
/// Self-contained first-run guided tour overlay. Highlights key UI elements
/// step-by-step with a spotlight + bubble. Skippable at every step; writes a
/// marker file when finished so it only runs once per install. Purely a view —
/// no telemetry, no gating. The host can react to <see cref="StepShowing"/> to
/// switch tabs / expand panels so the target is visible before it is measured.
/// </summary>
public partial class TourOverlay : UserControl
{
    private static readonly LocalizationManager L = LocalizationManager.Instance;

    /// <summary>Raised when the tour finishes or is skipped.</summary>
    public event Action? TourCompleted;

    /// <summary>Raised before each step is measured; the step index is passed so
    /// the host can bring the target into view (e.g. select a tab, expand a panel).</summary>
    public event Action<int>? StepShowing;

    private int _tourStep = -1;
    private FrameworkElement[]? _tourTargets;

    private static string TourMarkerPath => System.IO.Path.Combine(AppPaths.Root, "tour_done");

    /// <summary>True once the tour marker file exists (tour completed in a prior session).</summary>
    public static bool TourCompletedStatic => File.Exists(TourMarkerPath);

    public TourOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Launch the tour. <paramref name="targets"/> are the UI elements to spotlight
    /// in order; one step per target.
    /// </summary>
    public void StartTour(FrameworkElement[] targets)
    {
        _tourTargets = targets;
        _tourStep = 0;
        Visibility = Visibility.Visible;
        ApplyTourStep();
    }

    /// <summary>Hide the tour overlay immediately (does not mark completion).</summary>
    public void HideTour()
    {
        _tourStep = -1;
        Visibility = Visibility.Collapsed;
    }

    private void TourNext_Click(object s, RoutedEventArgs e)
    {
        if (_tourStep < 0) return;
        _tourStep++;
        if (_tourTargets == null || _tourStep >= _tourTargets.Length) { EndTour(); return; }
        ApplyTourStep();
    }

    private void TourSkip_Click(object s, RoutedEventArgs e) => EndTour();

    private void TourOverlay_Click(object s, MouseButtonEventArgs e)
    {
        // Clicking the dim area advances (ignore clicks inside the bubble).
        if (e.OriginalSource is DependencyObject d && IsDescendantOf(d, TourBubble)) return;
        if (_tourStep >= 0) TourNext_Click(s, e);
    }

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void ApplyTourStep()
    {
        if (_tourTargets == null || _tourStep < 0 || _tourStep >= _tourTargets.Length)
        {
            EndTour();
            return;
        }

        var target = _tourTargets[_tourStep];
        if (target == null) { EndTour(); return; }

        // Let the host bring the target into view (switch tab / expand panel) BEFORE measuring.
        try { StepShowing?.Invoke(_tourStep); } catch { /* host hook is best-effort */ }

        // The overlay's parent Grid is the measurement host.
        var host = Parent as Grid;
        if (host == null) { EndTour(); return; }

        int last = _tourTargets.Length - 1;

        // Recompute after layout so freshly-activated tab / expanded panel measurements are valid.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!target.IsVisible || target.ActualWidth <= 0 || target.ActualHeight <= 0)
                {
                    // Target still not laid out — skip this step rather than show a broken frame.
                    if (_tourStep < last) { _tourStep++; ApplyTourStep(); }
                    else EndTour();
                    return;
                }

                var t = target.TransformToVisual(host);
                var rect = t.TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
                const double pad = 8;
                var x = Math.Max(0, rect.X - pad);
                var y = Math.Max(0, rect.Y - pad);
                var w = Math.Min(host.ActualWidth - x, rect.Width + pad * 2);
                var h = Math.Min(host.ActualHeight - y, rect.Height + pad * 2);
                w = Math.Max(40, w);
                h = Math.Max(40, h);

                SetRect(TourDimTop, 0, 0, host.ActualWidth, y);
                SetRect(TourDimBottom, 0, y + h, host.ActualWidth, Math.Max(0, host.ActualHeight - y - h));
                SetRect(TourDimLeft, 0, y, x, h);
                SetRect(TourDimRight, x + w, y, Math.Max(0, host.ActualWidth - x - w), h);

                Canvas.SetLeft(TourHighlight, x);
                Canvas.SetTop(TourHighlight, y);
                TourHighlight.Width = w;
                TourHighlight.Height = h;

                TourStepTitle.Text = string.Format(L.TourTitle + "  {0}/{1}", _tourStep + 1, _tourTargets!.Length);
                TourStepBody.Text = StepBody(_tourStep);
                TourNextBtn.Content = _tourStep == last ? L.TourDone : L.OnbNext;

                TourBubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double bw = Math.Min(340, TourBubble.DesiredSize.Width);
                double bh = TourBubble.DesiredSize.Height;
                double bx = Math.Min(Math.Max(8, x + w / 2 - bw / 2), Math.Max(8, host.ActualWidth - bw - 8));
                double by = y + h + 12;
                if (by + bh > host.ActualHeight - 8)
                    by = Math.Max(8, y - bh - 12);
                Canvas.SetLeft(TourBubble, bx);
                Canvas.SetTop(TourBubble, by);
            }
            catch { EndTour(); }
        }), DispatcherPriority.Loaded);
    }

    /// <summary>Localized body text for a step index (mapped to the tour keys).</summary>
    private string StepBody(int step) => step switch
    {
        0 => L.TourStep1,
        1 => L.TourStep2,
        2 => L.TourStep3,
        _ => L.TourStep4,
    };

    private static void SetRect(Rectangle r, double x, double y, double w, double h)
    {
        r.Width = Math.Max(0, w);
        r.Height = Math.Max(0, h);
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
    }

    private void EndTour()
    {
        _tourStep = -1;
        Visibility = Visibility.Collapsed;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(TourMarkerPath);
            if (dir != null) Directory.CreateDirectory(dir);
            if (!File.Exists(TourMarkerPath)) File.WriteAllText(TourMarkerPath, DateTime.UtcNow.ToString("o"));
        }
        catch { /* tour marker write is best-effort */ }
        TourCompleted?.Invoke();
    }
}
