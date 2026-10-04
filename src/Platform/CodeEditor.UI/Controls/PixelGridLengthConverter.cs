using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CodeEditor.UI.Controls;

/// <summary>
/// <see cref="double"/> ↔ pixel <see cref="GridLength"/>: the ViewModel's area size and the column width a
/// <c>GridSplitter</c> changes.
/// </summary>
public sealed class PixelGridLengthConverter : IValueConverter
{
    public static PixelGridLengthConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double size ? new GridLength(size) : new GridLength(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GridLength length ? length.Value : 0d;
}
