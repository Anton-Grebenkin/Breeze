using System.Windows;
using System.Windows.Controls;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Title bar layout as in VS Code: left content (menu), a centered element (search) and right content (buttons). The
/// center stays in the middle of the window while it fits; otherwise it moves between the sides and shrinks down to its
/// <see cref="FrameworkElement.MinWidth"/>, so it never covers the menu (narrow window, interface zoom).
/// Children: [0] left, [1] center (preferred width = its <see cref="FrameworkElement.MaxWidth"/>), [2] right.
/// </summary>
public sealed class TitleBarPanel : Panel
{
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            width += child.DesiredSize.Width;
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count < 3)
        {
            return finalSize;
        }

        var (left, center, right) = (InternalChildren[0], InternalChildren[1], InternalChildren[2]);
        var rightWidth = Math.Min(right.DesiredSize.Width, finalSize.Width);
        var leftWidth = Math.Min(left.DesiredSize.Width, finalSize.Width - rightWidth);
        right.Arrange(new Rect(finalSize.Width - rightWidth, 0, rightWidth, finalSize.Height));
        left.Arrange(new Rect(0, 0, leftWidth, finalSize.Height));

        var (minWidth, preferred) = center is FrameworkElement element && !double.IsInfinity(element.MaxWidth)
            ? (element.MinWidth, element.MaxWidth)
            : (0, center.DesiredSize.Width);
        var space = finalSize.Width - leftWidth - rightWidth - 2 * Gap;
        if (space < minWidth)
        {
            // No room even for the narrowest search box: hide it rather than cover the menu or the buttons.
            center.Arrange(new Rect(0, 0, 0, 0));
            return finalSize;
        }

        var centerWidth = Math.Min(preferred, space);
        var x = Math.Clamp((finalSize.Width - centerWidth) / 2, leftWidth + Gap, finalSize.Width - rightWidth - Gap - centerWidth);
        center.Arrange(new Rect(x, 0, centerWidth, finalSize.Height));
        return finalSize;
    }
}
