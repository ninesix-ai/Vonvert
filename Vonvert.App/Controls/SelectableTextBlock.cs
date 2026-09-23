// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Vonvert.App.Controls;

/// <summary>
/// A read-only text display control that supports native text selection and copy
/// (Ctrl+C, Ctrl+A, drag-to-select). Drop-in replacement for TextBlock in XAML.
///
/// <para>
/// <b>Design rationale:</b> This control inherits from <see cref="FrameworkElement"/>
/// (NOT TextBox) so that its <see cref="TextProperty"/> defaults to <b>OneWay</b>
/// binding. TextBox.TextProperty carries BindsTwoWayByDefault, which causes
/// InvalidOperationException when binding to read-only source properties such as
/// LocalizationManager.AppName. By not inheriting TextBox, this entire class of
/// binding-mode bugs is eliminated at the architectural level.
/// </para>
/// </summary>
public class SelectableTextBlock : FrameworkElement
{
    // ── Dependency Properties ────────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged));

    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                new FontFamily("Segoe UI"),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                12.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                FontWeights.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStyleProperty =
        TextElement.FontStyleProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                FontStyles.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStretchProperty =
        TextElement.FontStretchProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                FontStretches.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnForegroundChanged));

    /// <summary>
    /// TextTrimming DP so XAML attributes like TextTrimming="CharacterEllipsis" compile.
    /// The actual trimming is handled by <see cref="BuildFormattedText"/>.
    /// </summary>
    public static readonly DependencyProperty TextTrimmingProperty =
        DependencyProperty.Register(
            nameof(TextTrimming), typeof(TextTrimming), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(TextTrimming.None,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping), typeof(TextWrapping), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// TextAlignment DP for XAML compatibility (inherited from TextBox in the old design).
    /// Currently only Left alignment is rendered; this property exists so existing XAML compiles.
    /// </summary>
    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(
            nameof(TextAlignment), typeof(TextAlignment), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Left,
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// BorderThickness DP for XAML style compatibility. The control renders no border.
    /// </summary>
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness), typeof(Thickness), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(new Thickness(0)));

    /// <summary>
    /// Background DP for XAML style compatibility. The control renders its own text background.
    /// </summary>
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background), typeof(Brush), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(Brushes.Transparent,
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Padding DP for XAML style compatibility.
    /// </summary>
    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding), typeof(Thickness), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(new Thickness(0),
                FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// TextDecorations DP for XAML compatibility (e.g. underline, strikethrough).
    /// </summary>
    public static readonly DependencyProperty TextDecorationsProperty =
        DependencyProperty.Register(
            nameof(TextDecorations), typeof(TextDecorationCollection), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// IsReadOnly DP for XAML style compatibility. Always returns true (this control is read-only).
    /// </summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(true));

    /// <summary>
    /// IsTabStop DP for XAML style compatibility.
    /// </summary>
    public static readonly DependencyProperty IsTabStopProperty =
        DependencyProperty.Register(
            nameof(IsTabStop), typeof(bool), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// VerticalContentAlignment DP for XAML style compatibility.
    /// </summary>
    public static readonly DependencyProperty VerticalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(VerticalContentAlignment), typeof(VerticalAlignment), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(VerticalAlignment.Top));

    /// <summary>
    /// HorizontalContentAlignment DP for XAML style compatibility.
    /// </summary>
    public static readonly DependencyProperty HorizontalContentAlignmentProperty =
        DependencyProperty.Register(
            nameof(HorizontalContentAlignment), typeof(HorizontalAlignment), typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left));

    // ── CLR Properties ───────────────────────────────────────────────

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public FontStretch FontStretch
    {
        get => (FontStretch)GetValue(FontStretchProperty);
        set => SetValue(FontStretchProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public TextTrimming TextTrimming
    {
        get => (TextTrimming)GetValue(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public TextDecorationCollection TextDecorations
    {
        get => (TextDecorationCollection)GetValue(TextDecorationsProperty);
        set => SetValue(TextDecorationsProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool IsTabStop
    {
        get => (bool)GetValue(IsTabStopProperty);
        set => SetValue(IsTabStopProperty, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => (VerticalAlignment)GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    // ── Selection state ──────────────────────────────────────────────

    // WPF TextFormatter throws ArgumentOutOfRangeException when paragraphWidth
    // is double.PositiveInfinity. Use a large finite cap instead.
    private const double MaxLayoutWidth = 1_000_000;

    private int _selStart = -1;
    private int _selEnd = -1;
    private bool _isDragging;
    private FormattedText? _cachedLayout;
    private double[] _charWidths = Array.Empty<double>();

    // ── Selection colors (system defaults) ───────────────────────────

    private static readonly Brush SelectionBrush = SystemColors.HighlightBrush;
    private static readonly Brush SelectionForeground = SystemColors.HighlightTextBrush;

    // ── Static constructor ───────────────────────────────────────────

    static SelectableTextBlock()
    {
        // Ensure the default style key is not looked up (we render in OnRender).
        FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock),
            new FrameworkPropertyMetadata(true));
    }

    public SelectableTextBlock()
    {
        FocusVisualStyle = null;
        Cursor = Cursors.IBeam;
        ContextMenuOpening += OnContextMenuOpeningHandler;
    }

    // ── Text layout ──────────────────────────────────────────────────

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SelectableTextBlock stb)
        {
            stb._cachedLayout = null;
            stb._charWidths = Array.Empty<double>();
            stb.InvalidateMeasure();
            stb.InvalidateVisual();
        }
    }

    private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // The cached FormattedText bakes in the old brush; drop it so the next
        // render picks up a new Foreground assigned at runtime.
        if (d is SelectableTextBlock stb)
        {
            stb._cachedLayout = null;
            stb.InvalidateVisual();
        }
    }

    /// <summary>
    /// Builds the <see cref="FormattedText"/> used for rendering and hit-testing.
    /// Handles TextTrimming.CharacterEllipsis by binary-searching the longest prefix.
    /// </summary>
    private FormattedText BuildFormattedText()
    {
        if (_cachedLayout != null) return _cachedLayout;

        var typeface = new Typeface(
            FontFamily ?? new FontFamily("Segoe UI"),
            FontStyle, FontWeight, FontStretch);

        var dpi = VisualTreeHelper.GetDpi(this);
        var fg = Foreground as SolidColorBrush ?? Brushes.Black;
        var text = Text ?? string.Empty;
        var fallbackWidth = ActualWidth > 0 ? ActualWidth : MaxLayoutWidth;

        var ft = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, FontSize, fg, dpi.PixelsPerDip);

        // Only constrain width when wrapping is enabled; NoWrap means single line.
        if (TextWrapping != TextWrapping.NoWrap)
            ft.MaxTextWidth = Math.Min(fallbackWidth, MaxLayoutWidth);

        // Handle CharacterEllipsis trimming.
        if (TextTrimming == TextTrimming.CharacterEllipsis
            && ft.WidthIncludingTrailingWhitespace > fallbackWidth
            && !string.IsNullOrEmpty(text))
        {
            const string ellipsis = "…";
            var lo = 0;
            var hi = text.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                var candidate = text[..mid] + ellipsis;
                var test = new FormattedText(
                    candidate, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, FontSize, fg, dpi.PixelsPerDip);
                if (test.WidthIncludingTrailingWhitespace <= fallbackWidth)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            ft = new FormattedText(
                text[..lo] + ellipsis, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, FontSize, fg, dpi.PixelsPerDip);
            ft.MaxTextWidth = Math.Min(fallbackWidth, MaxLayoutWidth);
        }

        _cachedLayout = ft;
        CacheCharWidths(ft, text);
        return ft;
    }

    /// <summary>
    /// Pre-compute per-character advance widths for selection rendering and hit-testing.
    /// </summary>
    private void CacheCharWidths(FormattedText ft, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _charWidths = Array.Empty<double>();
            return;
        }

        var typeface = new Typeface(
            FontFamily ?? new FontFamily("Segoe UI"),
            FontStyle, FontWeight, FontStretch);
        var dpi = VisualTreeHelper.GetDpi(this);
        _charWidths = new double[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var chFt = new FormattedText(
                text[i].ToString(), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, FontSize, Brushes.Black, dpi.PixelsPerDip);
            chFt.MaxTextWidth = MaxLayoutWidth;
            _charWidths[i] = chFt.WidthIncludingTrailingWhitespace;
        }
    }

    /// <summary>
    /// Returns the X offset of the character at <paramref name="index"/>.
    /// </summary>
    private double GetCharX(int index)
    {
        var x = 0.0;
        for (var i = 0; i < index && i < _charWidths.Length; i++)
            x += _charWidths[i];
        return x;
    }

    // ── Measure / Arrange ────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        // During initial layout ActualWidth is 0; use availableSize as fallback
        // so FormattedText never receives double.PositiveInfinity.
        var fallbackWidth = ActualWidth > 0 ? ActualWidth
            : (double.IsFinite(availableSize.Width) ? availableSize.Width : MaxLayoutWidth);

        var typeface = new Typeface(
            FontFamily ?? new FontFamily("Segoe UI"),
            FontStyle, FontWeight, FontStretch);
        var dpi = VisualTreeHelper.GetDpi(this);
        var fg = Foreground as SolidColorBrush ?? Brushes.Black;
        var text = Text ?? string.Empty;

        var ft = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, FontSize, fg, dpi.PixelsPerDip);

        // Only constrain width when wrapping is enabled; NoWrap means single line.
        if (TextWrapping != TextWrapping.NoWrap)
            ft.MaxTextWidth = Math.Min(fallbackWidth, MaxLayoutWidth);

        return new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
    }

    // ── Render ───────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var ft = BuildFormattedText();
        var text = Text ?? string.Empty;
        var textHeight = ft.Height;

        // Draw the text with alignment.
        var textX = ComputeAlignmentOffset();

        // Draw selection highlight behind text.
        if (_selStart >= 0 && _selEnd >= 0 && _selStart != _selEnd)
        {
            var lo = Math.Min(_selStart, _selEnd);
            var hi = Math.Max(_selStart, _selEnd);
            lo = Math.Clamp(lo, 0, text.Length);
            hi = Math.Clamp(hi, 0, text.Length);

            var selX = GetCharX(lo) + textX;
            var selW = GetCharX(hi) - GetCharX(lo);
            dc.DrawRectangle(SelectionBrush, null, new Rect(selX, 0, selW, textHeight));
        }

        dc.DrawText(ft, new Point(textX, 0));

        // Re-draw selected characters with selection foreground colour.
        if (_selStart >= 0 && _selEnd >= 0 && _selStart != _selEnd)
        {
            var lo = Math.Min(_selStart, _selEnd);
            var hi = Math.Max(_selStart, _selEnd);
            lo = Math.Clamp(lo, 0, text.Length);
            hi = Math.Clamp(hi, 0, text.Length);

            var typeface = new Typeface(
                FontFamily ?? new FontFamily("Segoe UI"),
                FontStyle, FontWeight, FontStretch);
            var dpi = VisualTreeHelper.GetDpi(this);

            for (var i = lo; i < hi; i++)
            {
                var x = GetCharX(i) + textX;
                var chFt = new FormattedText(
                    text[i].ToString(), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, FontSize,
                    SelectionForeground, dpi.PixelsPerDip);
                dc.DrawText(chFt, new Point(x, 0));
            }
        }
    }

    // ── Hit-testing / Selection ──────────────────────────────────────

    private int HitTestPosition(Point pt)
    {
        var text = Text ?? string.Empty;
        if (string.IsNullOrEmpty(text) || _charWidths.Length == 0) return 0;

        // Adjust for text alignment offset.
        var textX = ComputeAlignmentOffset();
        var adjustedX = pt.X - textX;

        // Walk cached character widths to find the position closest to adjustedX.
        var x = 0.0;
        for (var i = 0; i < _charWidths.Length; i++)
        {
            var mid = x + _charWidths[i] / 2;
            if (adjustedX < mid)
                return i;
            x += _charWidths[i];
        }
        return text.Length;
    }

    /// <summary>
    /// Compute the X offset for the current TextAlignment.
    /// </summary>
    private double ComputeAlignmentOffset()
    {
        var ft = BuildFormattedText();
        var offset = 0.0;
        if (TextAlignment == TextAlignment.Center)
            offset = (ActualWidth - ft.WidthIncludingTrailingWhitespace) / 2.0;
        else if (TextAlignment == TextAlignment.Right)
            offset = ActualWidth - ft.WidthIncludingTrailingWhitespace;
        return Math.Max(0, offset);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        var pos = HitTestPosition(e.GetPosition(this));

        if (e.ClickCount == 2)
        {
            // Double-click: select all text.
            var text = Text ?? string.Empty;
            _selStart = 0;
            _selEnd = text.Length;
            _isDragging = false;
        }
        else
        {
            _selStart = pos;
            _selEnd = pos;
            _isDragging = true;
        }

        InvalidateVisual();
        Focus();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging) return;
        _selEnd = HitTestPosition(e.GetPosition(this));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }
        e.Handled = true;
    }



    // ── Keyboard: Ctrl+A, Ctrl+C ─────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch (e.Key)
            {
                case Key.A:
                    // Select all.
                    var text = Text ?? string.Empty;
                    _selStart = 0;
                    _selEnd = text.Length;
                    InvalidateVisual();
                    e.Handled = true;
                    break;

                case Key.C:
                    CopySelectionToClipboard();
                    e.Handled = true;
                    break;
            }
        }
    }

    // ── Clipboard ────────────────────────────────────────────────────

    private void CopySelectionToClipboard()
    {
        var text = Text ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        string selected;
        if (_selStart >= 0 && _selEnd >= 0 && _selStart != _selEnd)
        {
            var lo = Math.Min(_selStart, _selEnd);
            var hi = Math.Max(_selStart, _selEnd);
            lo = Math.Clamp(lo, 0, text.Length);
            hi = Math.Clamp(hi, 0, text.Length);
            selected = text[lo..hi];
        }
        else
        {
            selected = text; // No selection → copy all.
        }

        if (!string.IsNullOrEmpty(selected))
            Clipboard.SetText(selected);
    }

    // ── Context menu: Copy ───────────────────────────────────────────

    private void OnContextMenuOpeningHandler(object sender, ContextMenuEventArgs e)
    {
        var menu = new ContextMenu();
        var copyItem = new MenuItem { Header = "Copy" };
        copyItem.Click += (_, _) => CopySelectionToClipboard();
        menu.Items.Add(copyItem);
        ContextMenu = menu;
    }
}
