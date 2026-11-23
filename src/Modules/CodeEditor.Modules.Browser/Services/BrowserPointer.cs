using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Real mouse events through DevTools (<c>Input.dispatchMouseEvent</c>), as a person's mouse makes them: the page sees
/// trusted <c>mousemove</c>, <c>mousedown</c>, <c>mouseup</c> and <c>click</c>, hover styles and menus work. Points are
/// in CSS pixels of the page's viewport.
/// </summary>
public static class BrowserPointer
{
    public const string Method = "Input.dispatchMouseEvent";

    /// <summary>Steps of a drag between two points: enough for a page that follows the mouse.</summary>
    public const int DragSteps = 8;

    public static string Move(double x, double y, bool pressed = false) => Event("mouseMoved", x, y, pressed ? 1 : 0, clicks: 0);

    public static string Press(double x, double y) => Event("mousePressed", x, y, buttons: 1, clicks: 1);

    public static string Release(double x, double y) => Event("mouseReleased", x, y, buttons: 0, clicks: 1);

    /// <summary>The events of a click at the point: the mouse comes, presses and releases.</summary>
    public static IReadOnlyList<string> Click(double x, double y) => [Move(x, y), Press(x, y), Release(x, y)];

    /// <summary>The events of dragging from one point to another with the button held.</summary>
    public static IReadOnlyList<string> Drag(double fromX, double fromY, double toX, double toY)
    {
        var events = new List<string>(DragSteps + 3) { Move(fromX, fromY), Press(fromX, fromY) };
        for (var step = 1; step <= DragSteps; step++)
        {
            var share = step / (double)DragSteps;
            events.Add(Move(fromX + (toX - fromX) * share, fromY + (toY - fromY) * share, pressed: true));
        }

        events.Add(Release(toX, toY));
        return events;
    }

    private static string Event(string type, double x, double y, int buttons, int clicks) => new JsonObject
    {
        ["type"] = type,
        ["x"] = Math.Round(x, 1),
        ["y"] = Math.Round(y, 1),
        ["button"] = type == "mouseMoved" && buttons == 0 ? "none" : "left",
        ["buttons"] = buttons,
        ["clickCount"] = clicks,
    }.ToJsonString();
}
