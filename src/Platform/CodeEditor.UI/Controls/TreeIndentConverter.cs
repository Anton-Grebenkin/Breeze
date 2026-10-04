using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CodeEditor.UI.Controls;

/// <summary>
/// Tree row indent by node depth: the selection spans the full row width and only the content shifts.
/// </summary>
public sealed class TreeIndentConverter : IValueConverter
{
    public const double IndentPerLevel = 10;

    // Content starts right of the selection bar (Trees.xaml): 4 px background inset plus a 3 px bar.
    private const double BaseIndent = 8;

    public static TreeIndentConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(BaseIndent + DepthOf(value as TreeViewItem) * IndentPerLevel, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static int DepthOf(TreeViewItem? item)
    {
        var depth = 0;
        for (var parent = item is null ? null : ItemsControl.ItemsControlFromItemContainer(item);
             parent is TreeViewItem treeViewItem;
             parent = ItemsControl.ItemsControlFromItemContainer(treeViewItem))
        {
            depth++;
        }

        return depth;
    }
}
