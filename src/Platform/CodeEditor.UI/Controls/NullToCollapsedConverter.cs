using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CodeEditor.UI.Controls;

/// <summary>
/// <c>null</c> or an empty string → <see cref="Visibility.Collapsed"/>, otherwise <see cref="Visibility.Visible"/>.
/// </summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public static NullToCollapsedConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
