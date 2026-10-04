using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CodeEditor.UI.Controls;

/// <summary>
/// An icon font glyph drawn as geometry with an optional outline. Codicons are drawn for a 16 px grid: as text at
/// 20–24 px their 1.25–1.5 px lines get uneven anti-aliasing and look jagged. The glyph outline plus a thin stroke of
/// the same color gives even lines, like icons in a browser. Font and color are inherited, like text.
/// </summary>
public sealed class GlyphIcon : FrameworkElement
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(GlyphIcon),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnShapeChanged));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(GlyphIcon),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnShapeChanged));

    public static readonly DependencyProperty WeightProperty = DependencyProperty.Register(
        nameof(Weight), typeof(double), typeof(GlyphIcon),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(GlyphIcon),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender, OnShapeChanged));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(GlyphIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    private Geometry? _shape;

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Em size in device-independent pixels.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Stroke added around the outline, in pixels: 0.3–0.5 evens out thin lines.</summary>
    public double Weight
    {
        get => (double)GetValue(WeightProperty);
        set => SetValue(WeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if ((_shape ??= Outline()) is not { } shape)
        {
            return;
        }

        var pen = Weight > 0 ? new Pen(Foreground, Weight) { LineJoin = PenLineJoin.Round } : null;
        drawingContext.DrawGeometry(Foreground, pen, shape);
    }

    private static void OnShapeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((GlyphIcon)sender)._shape = null;

    // The glyph outline in a Size×Size em box with the origin at its top-left corner.
    private Geometry? Outline()
    {
        if (string.IsNullOrEmpty(Glyph)
            || !new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyphs)
            || !glyphs.CharacterToGlyphMap.TryGetValue(char.ConvertToUtf32(Glyph, 0), out var index))
        {
            return null;
        }

        var outline = new GeometryGroup { FillRule = FillRule.Nonzero, Transform = new TranslateTransform(0, glyphs.Baseline * Size) };
        outline.Children.Add(glyphs.GetGlyphOutline(index, Size, Size));
        outline.Freeze();
        return outline;
    }
}
