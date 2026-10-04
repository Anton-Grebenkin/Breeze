using System.Globalization;
using System.Windows.Data;
using CodeEditor.UI.Themes;

namespace CodeEditor.UI.Controls;

/// <summary>Codicon name → <c>Font.Icons</c> glyph: <c>{Binding Icon, Converter=…}</c>.</summary>
public sealed class IconGlyphConverter : IValueConverter
{
    public static IconGlyphConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Codicons.Glyph(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
