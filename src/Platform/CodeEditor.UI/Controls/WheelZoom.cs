using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CodeEditor.UI.Controls;

/// <summary>
/// Marks an element that zooms its own content with Ctrl + mouse wheel (the editor font, a picture). Elsewhere the
/// main window turns Ctrl + wheel into interface zoom; inside a marked element it leaves the wheel to the element.
/// </summary>
public static class WheelZoom
{
    public static readonly DependencyProperty HasOwnZoomProperty = DependencyProperty.RegisterAttached(
        "HasOwnZoom", typeof(bool), typeof(WheelZoom), new PropertyMetadata(false));

    public static bool GetHasOwnZoom(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(HasOwnZoomProperty);
    }

    public static void SetHasOwnZoom(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(HasOwnZoomProperty, value);
    }

    /// <summary>Whether the element or one of its ancestors zooms with the wheel itself. O(tree depth).</summary>
    public static bool IsWithinOwnZoom(DependencyObject? element)
    {
        for (var current = element; current is not null; current = ParentOf(current))
        {
            if (GetHasOwnZoom(current))
            {
                return true;
            }
        }

        return false;
    }

    // Text elements (a Run in a document) have no visual parent, only a logical one.
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
