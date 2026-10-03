// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vonvert.App;
using Vonvert.App.UIServices;
using Vonvert.Engine;
using Xunit;

namespace Vonvert.Tests.UIAutomation;

// ═══════════════════════════════════════════════════════════════════
//  MF-001, MF-003 ~ MF-005: proof of what the monitor actually paints, starting from its three
//  text sizes. There is no MF-002: "the three pictures must differ from each other" became an
//  assertion inside MF-001 rather than a test of its own, and the ids were never renumbered
//  because MF-004 is already published and cited by number in the plan document.
//
//  FS-001..008 check the numbers in the table; a table can be right while the screen is
//  wrong (a binding that never resolves, a size that fits on one line and clips on the
//  next). These tests host the real window on a WPF thread, let it lay out and paint, then
//  write one PNG per case plus the measured boxes that WPF settled on - so the difference is
//  audited as rendered pixels and as layout facts, not as an assertion about a constant.
//
//  It grew past text sizes because that is where it earned its keep: MF-004 found the corner
//  toolbar wrapping over the line in the middle of the window, and MF-005 checks the view
//  buttons added later, whose popup is a separate window that can end up off screen or clipping
//  a long translation. Anything about this window whose correctness is geometric belongs here.
//
//  Deliberately excluded from the ordinary run (namespace filter UIAutomation): it opens a
//  window. It redirects the data root to a temp folder and leaves a tour-seen marker there,
//  so it never touches the developer's real settings and never captures the walkthrough
//  overlay in the picture. Because that redirect is one process-wide static, the class joins
//  the AppPathsSeam collection like every other test that moves the data root.
// ═══════════════════════════════════════════════════════════════════
[Collection("AppPathsSeam")]
public sealed class MonitorFontScaleCaptureTests
{
    private static readonly MonitorFontScale[] Sizes =
        { MonitorFontScale.Compact, MonitorFontScale.Standard, MonitorFontScale.Large };

    [InteractiveDesktopFact(DisplayName = "MF-001: each text size renders, measures and captures differently")]
    public void MF001_CapturesEverySizeAndMeasuresTheLayout()
    {
        var outputDir = OutputDirectory();
        var measurements = new List<string>();
        var heights = new Dictionary<MonitorFontScale, double>();
        Exception? failure = null;

        var worker = new Thread(() =>
        {
            try { CaptureAll(outputDir, measurements, heights); }
            catch (Exception ex) { failure = ex; }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromMinutes(3)), "the WPF capture thread did not finish in time");

        Assert.True(failure is null, "capture failed: " + failure?.ToString());

        // Every picture must exist and be a real window, not an empty file.
        foreach (var size in Sizes)
        {
            var png = Path.Combine(outputDir, $"monitor-{size.ToString().ToLowerInvariant()}.png");
            Assert.True(File.Exists(png), $"{png} was not written");
            Assert.True(new FileInfo(png).Length > 20_000,
                $"{png} is {new FileInfo(png).Length} bytes; a real 1280x720 window cannot be that small");
        }

        // Every picture must be a different picture, or the setting is decoration. This is the
        // assertion that caught the harness photographing the same size three times.
        var hashes = Sizes
            .Select(size => Path.Combine(outputDir, $"monitor-{size.ToString().ToLowerInvariant()}.png"))
            .Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))
            .ToList();
        Assert.Equal(3, hashes.Distinct().Count());

        // The layout itself has to differ - a size that does not change the measured text box
        // is a setting that does nothing.
        Assert.True(heights[MonitorFontScale.Compact] < heights[MonitorFontScale.Standard],
            $"compact measured {heights[MonitorFontScale.Compact]:F1} px vs standard {heights[MonitorFontScale.Standard]:F1} px");
        Assert.True(heights[MonitorFontScale.Standard] < heights[MonitorFontScale.Large],
            $"standard measured {heights[MonitorFontScale.Standard]:F1} px vs large {heights[MonitorFontScale.Large]:F1} px");

        // MF-002: the jump the user actually buys, from rendered layout rather than constants.
        double gain = heights[MonitorFontScale.Large] / heights[MonitorFontScale.Standard];
        Assert.True(gain >= 1.25, $"large only grows the measured hint chip to {gain:F2}x");

        measurements.Add("sha256 " + string.Join("  ", Sizes.Select(size => $"{size}={Short(outputDir, size)}")));
        // No BOM: every text file in this project is UTF-8 without one, generated reports included.
        File.WriteAllLines(Path.Combine(outputDir, "measured.txt"), measurements, new UTF8Encoding(false));
    }

    private static string Short(string outputDir, MonitorFontScale size)
    {
        var png = Path.Combine(outputDir, $"monitor-{size.ToString().ToLowerInvariant()}.png");
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(png)))[..12].ToLowerInvariant();
    }

    [Fact(DisplayName = "MF-003: the output directory is reported, so the pictures can be reviewed")]
    public void MF003_OutputDirectoryIsAdvisable()
    {
        // Not a behaviour test - a guard that the harness does not scatter files into the repo.
        var dir = OutputDirectory();
        Assert.False(dir.Contains(Path.Combine("Vonvert.App", "Translations")), dir);
        Assert.Contains("tmp", dir);
    }

    /// <summary>
    /// The bottom of the window carries four independent overlays: the close hint on the left,
    /// the collapse strips and the gesture pill in the middle, and the chrome toolbar on the
    /// right. A user reported that the toolbar grew to two rows and covered the text in the
    /// middle, so the rectangles are compared here rather than eyeballed - and the picture is
    /// written out so the report can be checked by eye too.
    /// </summary>
    [InteractiveDesktopFact(DisplayName = "MF-004: the bottom overlays never sit on top of each other")]
    public void MF004_BottomOverlaysDoNotCollide()
    {
        var outputDir = OutputDirectory();
        var report = new List<string>();
        var overlaps = new List<string>();
        Exception? failure = null;

        var worker = new Thread(() =>
        {
            try { MeasureBottomBand(outputDir, report, overlaps); }
            catch (Exception ex) { failure = ex; }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromMinutes(2)), "the WPF measurement thread did not finish");

        Assert.True(failure is null, "measurement failed: " + failure);
        File.WriteAllLines(Path.Combine(outputDir, "bottom-band.txt"), report, new UTF8Encoding(false));
        Assert.True(overlaps.Count == 0, string.Join("; ", overlaps));
    }

    /// <summary>
    /// The appearance popup at the size where it is most likely to fail: big text, a small
    /// window, and the four view buttons that were added later than the rest. Geometry is
    /// measured rather than assumed because the popup is a separate HWND placed above a corner
    /// button - it can run off the top of the screen, and a stretched label can be cut off in
    /// the languages whose words are longer than English.
    /// </summary>
    [InteractiveDesktopFact(DisplayName = "MF-005: the views popup lays out, marks the active view and fits the screen")]
    public void MF005_AppearancePopupLaysOut()
    {
        var outputDir = OutputDirectory();
        var report = new List<string>();
        var problems = new List<string>();
        Exception? failure = null;

        var worker = new Thread(() =>
        {
            try { MeasurePopup(outputDir, report, problems); }
            catch (Exception ex) { failure = ex; }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromMinutes(2)), "the WPF measurement thread did not finish");

        Assert.True(failure is null, "measurement failed: " + failure);
        File.WriteAllLines(Path.Combine(outputDir, "appearance-popup.txt"), report, new UTF8Encoding(false));
        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    private static void MeasurePopup(string outputDir, List<string> report, List<string> problems)
    {
        var root = Path.Combine(Path.GetTempPath(), "vonvert-popup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AppPaths.RootOverride = root;
        AppPaths.Invalidate();
        try { File.WriteAllText(MonitorFirstRunGuide.MarkerPath, "measured"); } catch { /* not fatal */ }

        var app = HostedApplication();
        // Saved, not assigned: the window restores its preferences on construction, which is the
        // only reason this harness ever produced three identical pictures.
        MonitorStore.Save(new MonitorPreferences
        {
            FontScale = MonitorFontScale.Large,
            Template = MonitorTemplate.Stream,
        });

        var window = new FullscreenVisualizationWindow { Width = 900, Height = 500, Topmost = false };
        window.Show();
        Pump(40);
        if (window.FindName("ChromeBar") is FrameworkElement chrome)
        {
            chrome.Opacity = 1;
            chrome.IsHitTestVisible = true;
        }
        if (window.FindName("AppearancePopup") is not Popup popup)
        {
            problems.Add("900x500: there is no AppearancePopup to open");
            window.Close();
            AppPaths.RootOverride = null;
            AppPaths.Invalidate();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            return;
        }
        popup.IsOpen = true;
        Pump(10);

        var names = new[] { "TemplateDiagnoseBtn", "TemplateStreamBtn", "TemplateLoudnessBtn", "TemplateTeachingBtn" };
        var buttons = names.Select(n => window.FindName(n) as Button).ToList();
        var boxes = new Dictionary<string, Rect>();
        for (int i = 0; i < names.Length; i++)
        {
            if (buttons[i] is null) { problems.Add($"{names[i]} is missing from the window"); continue; }
            boxes[names[i]] = OnScreen(buttons[i]);
            report.Add($"{names[i]}: {buttons[i]!.ActualWidth:F0}x{buttons[i]!.ActualHeight:F0} DIP");

            // Arranged smaller than measured is what a clipped label looks like in WPF numbers.
            var text = VisualChildText(buttons[i]!);
            if (text is not null && text.ActualHeight + 1 < text.DesiredSize.Height)
                problems.Add($"{names[i]}: label clipped ({text.ActualHeight:F0} of {text.DesiredSize.Height:F0} DIP)");
            if (buttons[i]!.ActualWidth < 60)
                problems.Add($"{names[i]}: {buttons[i]!.ActualWidth:F0} DIP wide, too narrow to read");
        }

        foreach (var (a, boxA) in boxes)
            foreach (var (b, boxB) in boxes)
            {
                if (string.CompareOrdinal(a, b) >= 0) continue;
                if (boxA.IntersectsWith(boxB))
                    problems.Add($"{a} overlaps {b}");
            }

        // The active view has to be the one the stored template selected, or the popup reports a
        // state the screen is not showing - the failure a marker control is most likely to have.
        // StyleToString only ever says "System.Windows.Style", so the styles themselves are
        // compared against the two the window swaps between.
        var activeStyle = window.TryFindResource("SegmentButtonActive");
        for (int i = 0; i < names.Length; i++)
        {
            if (buttons[i] is null) continue;
            bool isMarked = ReferenceEquals(buttons[i]!.Style, activeStyle);
            bool expectActive = MonitorTemplates.Order[i] == MonitorTemplate.Stream;
            if (isMarked != expectActive)
                problems.Add($"{names[i]} marked={isMarked} while the stored view is Stream");
        }

        var popupBox = OnScreen(popup.Child as FrameworkElement);
        report.Add($"popup: {popupBox.Width:F0}x{popupBox.Height:F0}@{popupBox.Left:F0},{popupBox.Top:F0}"
                   + $"  window {window.ActualWidth:F0}x{window.ActualHeight:F0}");
        var work = SystemParameters.WorkArea;
        if (!popupBox.IsEmpty && (popupBox.Top < work.Top - 1 || popupBox.Bottom > work.Bottom + 1))
            problems.Add("the popup does not fit the screen");

        // The popup is its own HWND, so a picture of the window alone would not show it.
        SavePng(window, Path.Combine(outputDir, "appearance-popup.png"));
        if (popup.Child is Visual popupVisual)
            SavePng(popupVisual, Path.Combine(outputDir, "appearance-popup-only.png"));
        popup.IsOpen = false;
        window.Close();
        Pump(5);

        AppPaths.RootOverride = null;
        AppPaths.Invalidate();
        Dispatcher.CurrentDispatcher.InvokeShutdown();
        GC.KeepAlive(app);
    }

    /// <summary>The first text block inside an element, so its measured height can be compared
    /// with the height it was actually given.</summary>
    private static TextBlock? VisualChildText(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock text) return text;
            if (VisualChildText(child) is { } found) return found;
        }
        return null;
    }

    private static void MeasureBottomBand(string outputDir, List<string> report, List<string> overlaps)
    {
        var root = Path.Combine(Path.GetTempPath(), "vonvert-band-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AppPaths.RootOverride = root;
        AppPaths.Invalidate();
        try { File.WriteAllText(MonitorFirstRunGuide.MarkerPath, "measured"); } catch { /* not fatal */ }

        var app = HostedApplication();

        // The narrow case is where a wrapping toolbar runs out of room; the wide one is the
        // report's own setup. Portrait and square are here because they are geometry this window
        // has never been drawn in, and "it scales proportionally" is a claim about numbers, not
        // about the picture.
        var cases = new (MonitorCanvasShape Shape, double Width, double Height)[]
        {
            (MonitorCanvasShape.Widescreen, 1280, 720),
            (MonitorCanvasShape.Widescreen, 900, 500),
            (MonitorCanvasShape.Widescreen, 640, 360),
            (MonitorCanvasShape.Vertical, 720, 1280),
            (MonitorCanvasShape.Square, 900, 900),
        };

        foreach (var (shape, width, height) in cases)
        {
            MonitorStore.Save(new MonitorPreferences { Shape = shape });
            var window = new FullscreenVisualizationWindow { Width = width, Height = height, Topmost = false };
            window.Show();
            Pump(40);

            // Reveal what a moving mouse reveals, including the toolbar the user saw.
            var chrome = window.FindName("ChromeBar") as FrameworkElement;
            if (chrome is not null)
            {
                chrome.Opacity = 1;
                chrome.IsHitTestVisible = true;
            }
            Pump(3);

            var boxes = new Dictionary<string, Rect>
            {
                ["chrome"] = OnScreen(chrome),
                ["pill"] = OnScreen(window.FindName("GesturePill") as FrameworkElement),
                ["collapse"] = OnScreen(window.FindName("CollapseBar") as FrameworkElement),
                ["hint"] = OnScreen(window.FindName("CloseHint") as FrameworkElement),
            };
            report.Add($"{shape} {width:F0}x{height:F0}: " + string.Join(
                "  ", boxes.Select(b => $"{b.Key}={b.Value.Width:F0}x{b.Value.Height:F0}@{b.Value.Left:F0},{b.Value.Top:F0}")));

            // The panels themselves, in DIP: the strip widths are derived from the window size,
            // so this is where a shape that was asked for but not delivered shows up, and eyeball
            // reading a scaled PNG is not a measurement.
            report.Add("    " + string.Join("  ", new[]
            {
                ("window", window),
                ("waterfall", window.FindName("WaterfallPanel") as FrameworkElement),
                ("waveform", window.FindName("WaveformPanel") as FrameworkElement),
                ("loudness", window.FindName("LoudnessPanel") as FrameworkElement),
            }.Select(p => $"{p.Item1}={p.Item2?.ActualWidth ?? 0:F0}x{p.Item2?.ActualHeight ?? 0:F0}")));

            // Proportional strips are the whole point of the geometry rules; if a strip wins the
            // window at some shape, the picture the feature exists to show stops being readable.
            if (window.FindName("WaterfallPanel") is FrameworkElement main && main.ActualWidth < width * 0.6)
                overlaps.Add($"{shape} {width:F0}x{height:F0}: voice detail squeezed to {main.ActualWidth:F0} DIP by the strips");
            if (window.FindName("WaveformPanel") is FrameworkElement wave && wave.ActualHeight < 60)
                overlaps.Add($"{shape} {width:F0}x{height:F0}: voice shape row is only {wave.ActualHeight:F0} DIP tall");

            // The complaint was specifically that the corner strip had become two rows, so the
            // height is pinned at the size the report was made at, not only the overlap.
            if (shape == MonitorCanvasShape.Widescreen && width >= 1280 && chrome is { ActualHeight: > 40 })
                overlaps.Add($"{width:F0}x{height:F0}: the toolbar is {chrome.ActualHeight:F0} DIP tall, so it wrapped to a second row");

            // A window that cannot keep its shape is not the shape that was asked for: this is
            // where the shipped 640x360 floor used to quietly override a portrait canvas.
            double ratio = width / height;
            if (Math.Abs(OnScreen(window).Width / OnScreen(window).Height - ratio) > 0.02)
                overlaps.Add($"{shape} {width:F0}x{height:F0}: drawn at {OnScreen(window).Width:F0}x{OnScreen(window).Height:F0}, not the requested shape");

            foreach (var (name, box) in boxes)
                foreach (var (other, otherBox) in boxes)
                {
                    if (name.CompareTo(other) >= 0) continue;      // compare each pair once
                    if (!box.IntersectsWith(otherBox)) continue;
                    var clip = Rect.Intersect(box, otherBox);
                    overlaps.Add($"{shape} {width:F0}x{height:F0}: {name} covers {other} by {clip.Width:F0}x{clip.Height:F0} px");
                }

            var picture = SavePng(window, Path.Combine(outputDir, $"bottom-band-{shape}-{width:F0}x{height:F0}.png"));
            // The picture is the artifact a human reviews, so it has to carry the shape too: a
            // fixed-size canvas once made a 9:16 window look like a broken landscape one.
            if (Math.Abs(picture.Width / (double)Math.Max(1, picture.Height) - width / height) > 0.02)
                overlaps.Add($"{shape} {width:F0}x{height:F0}: the picture is {picture.Width}x{picture.Height}");
            window.Close();
            Pump(5);
        }

        AppPaths.RootOverride = null;
        AppPaths.Invalidate();
        Dispatcher.CurrentDispatcher.InvokeShutdown();
        GC.KeepAlive(app);
    }

    /// <summary>A element's box in screen pixels; an element that is not on screen reports an
    /// empty rect, which cannot overlap anything.</summary>
    private static Rect OnScreen(FrameworkElement? element)
    {
        if (element is null || !element.IsVisible || element.ActualWidth <= 0) return Rect.Empty;
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    // ── the WPF side ────────────────────────────────────────────────────

    private static void CaptureAll(string outputDir, List<string> measurements,
                                   Dictionary<MonitorFontScale, double> heights)
    {
        // A data root of our own, so the temp run cannot write into the developer's settings.
        var root = Path.Combine(Path.GetTempPath(), "vonvert-fontscale-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AppPaths.RootOverride = root;
        AppPaths.Invalidate();

        // Seen marker where the window looks for it, so the walkthrough does not open on top
        // of the very text we are trying to photograph.
        try { File.WriteAllText(MonitorFirstRunGuide.MarkerPath, "captured"); }
        catch (Exception ex) { measurements.Add("note: marker not pre-set: " + ex.Message); }

        var app = HostedApplication();     // the styles the monitor inherits live in the app dictionaries

        foreach (var size in Sizes)
        {
            // Save it the way the button does, then let the window read it back. Setting only
            // the in-memory singleton is not enough and proved it: the window restores its
            // preferences on construction, so every capture came out byte-identical until this
            // line went through the store. The harness has to follow the real path or it
            // photographs a setting that never reaches the screen.
            MonitorStore.Save(new MonitorPreferences { FontScale = size });
            LocalizationManager.Instance.SetMonitorFontScale(size);

            var window = new FullscreenVisualizationWindow
            {
                Width = 1280,
                Height = 720,
                Topmost = false,
                Title = $"Vonvert monitor capture - {size}",
            };
            window.Show();
            Pump(60);              // several ticks of the 30 fps render timer

            var chip = window.FindName("WaveformHintChip") as FrameworkElement
                ?? throw new InvalidOperationException("WaveformHintChip is not in the window any more");
            var readout = window.FindName("LufsTruePeakText") as FrameworkElement
                ?? throw new InvalidOperationException("LufsTruePeakText is not in the window any more");

            heights[size] = chip.ActualHeight;
            measurements.Add(string.Format(CultureInfo.InvariantCulture,
                "{0,-9} chip {1:F1}px x {2:F1}px   readout {3:F1}px x {4:F1}px   configured {5}px",
                size, chip.ActualWidth, chip.ActualHeight,
                readout.ActualWidth, readout.ActualHeight,
                MonitorFontSizes.ResourcesFor(size)["FsFontBody"]));

            var png = Path.Combine(outputDir, $"monitor-{size.ToString().ToLowerInvariant()}.png");
            SavePng(window, png);

            window.Close();
            Pump(5);
        }

        AppPaths.RootOverride = null;
        AppPaths.Invalidate();
        Dispatcher.CurrentDispatcher.InvokeShutdown();
        GC.KeepAlive(app);
    }

    private static readonly object AppGate = new();
    private static Application? s_app;

    /// <summary>
    /// A bare Application has no styles, and the monitor's buttons resolve SegmentButton from
    /// the application dictionaries. They are loaded by pack URI instead of starting the real
    /// App, which would open the tray icon, the audio engine and the single-instance mutex
    /// inside a test process.
    /// </summary>
    private static Application HostedApplication()
    {
        // One Application per AppDomain is all WPF allows, and this class runs two tests that
        // each need it on their own STA thread. Windows keep their own dispatcher, so sharing
        // the instance (and its merged dictionaries) is enough.
        lock (AppGate)
        {
            if (s_app is not null) return s_app;

            // WPF resolves a pack URI by calling Assembly.Load on the assembly name, and in a test
            // host that misses the already-loaded Vonvert.App (the entry assembly is the runner).
            // Point the name at the instance we are holding instead of pretending to control
            // Application.ResourceAssembly, which refuses to change once WPF has set it.
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                args.Name.StartsWith("Vonvert.App", StringComparison.Ordinal)
                    ? typeof(FullscreenVisualizationWindow).Assembly
                    : null;

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            // Closing a captured window would otherwise take the whole Application down with it
            // (the default is OnLastWindowClose), and the later sizes would never render.
            foreach (string path in new[]
            {
                "AppStyles/Theme.xaml",
                "AppStyles/Controls.xaml",
                "AppStyles/SoundboardPadStyles.xaml",
                "Assets/Icons.xaml",
            })
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Vonvert.App;component/" + path, UriKind.Absolute),
                });
            }
            s_app = app;
            return s_app;
        }
    }

    /// <summary>
    /// Render a visual into a PNG at its own size, and report the size used. The canvas used to
    /// be a hard-coded 1280x720, which silently cropped every non-widescreen capture: the
    /// portrait picture came out landscape with dead space on the right, so the one artifact
    /// meant for human review was lying about the geometry the tests had measured correctly.
    /// </summary>
    private static (int Width, int Height) SavePng(Visual visual, string path)
    {
        int width = visual is FrameworkElement { ActualWidth: > 1 } element
            ? (int)Math.Round(element.ActualWidth)
            : (int)Math.Ceiling(VisualTreeHelper.GetDescendantBounds(visual).Width);
        int height = visual is FrameworkElement { ActualHeight: > 1 } sized
            ? (int)Math.Round(sized.ActualHeight)
            : (int)Math.Ceiling(VisualTreeHelper.GetDescendantBounds(visual).Height);
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return (width, height);
    }

    /// <summary>Let the dispatcher work through a number of render passes.</summary>
    private static void Pump(int passes)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        for (int i = 0; i < passes; i++)
        {
            dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render).Wait();
            Thread.Sleep(16);
        }
    }

    /// <summary>Somewhere under the workspace's tmp folder, never inside the repo.</summary>
    private static string OutputDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        var root = Path.Combine(dir!.FullName, "..", "..", "Vonvert_gitee", "tmp", "fontscale");
        Directory.CreateDirectory(root);
        return Path.GetFullPath(root);
    }
}
