using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Guarded clicks (<see cref="InputGuard"/>): the app window, not another window covering it, must be under the click
/// point. Replaces FlaUI's <c>AutomationElement.Click()</c> and <c>Mouse.DoubleClick</c>.
/// </summary>
public static class GuardedMouse
{
    /// <summary>Move after pressing, beyond WPF's drag start threshold.</summary>
    private const int DragStartOffset = 12;

    public static void GuardedClick(this AutomationElement element) => Press(PointOf(element), () => Mouse.LeftClick());

    public static void GuardedDoubleClick(this AutomationElement element) => DoubleClick(PointOf(element));

    public static void GuardedRightClick(this AutomationElement element) => Press(PointOf(element), () => Mouse.RightClick());

    public static void DoubleClick(Point point) => Press(point, () => Mouse.LeftDoubleClick());

    /// <summary>
    /// Drag: press on the element, move past the drag threshold, move to the target (so <c>DoDragDrop</c> sees motion
    /// over it), then release.
    /// </summary>
    public static void GuardedDrag(this AutomationElement element, Point target)
    {
        var start = PointOf(element);
        InputGuard.EnsurePointerTarget(start);
        InputGuard.EnsurePointerTarget(target);
        Mouse.Position = start;
        Mouse.Down(MouseButton.Left);
        Mouse.MoveBy(DragStartOffset, 0);
        Wait.UntilInputIsProcessed();
        Mouse.MoveTo(target);
        Wait.UntilInputIsProcessed();
        Mouse.Up(MouseButton.Left);
        Wait.UntilInputIsProcessed();
    }

    private static void Press(Point point, Action click)
    {
        InputGuard.EnsurePointerTarget(point);
        Mouse.Position = point;
        click();
        Wait.UntilInputIsProcessed();
    }

    private static Point PointOf(AutomationElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.TryGetClickablePoint(out var point))
        {
            return point;
        }

        var bounds = element.BoundingRectangle;
        return new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }
}
