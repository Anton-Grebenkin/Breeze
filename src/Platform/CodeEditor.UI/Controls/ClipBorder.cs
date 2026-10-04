using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeEditor.UI.Controls;

/// <summary>
/// A card border that clips its content to its rounded corners. A plain <see cref="Border"/> rounds only background and
/// stroke, so an editor or list inside draws square corners over it. Win32 content (WebView2, console) can't be clipped
/// and keeps square corners.
/// </summary>
public sealed class ClipBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (Child is not null)
        {
            Child.Clip = InnerClip(Child.RenderSize);
        }

        return arranged;
    }

    // Inner radius = outer radius minus stroke, otherwise content pokes out past the border line.
    private RectangleGeometry InnerClip(Size childSize)
    {
        var radius = Math.Max(0, CornerRadius.TopLeft - BorderThickness.Left);
        var clip = new RectangleGeometry(new Rect(childSize), radius, radius);
        clip.Freeze();
        return clip;
    }
}
