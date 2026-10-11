using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Keys the agent can press (<c>press</c>), as DevTools <c>Input.dispatchKeyEvent</c> parameters: a real key press, so
/// Enter submits a form, Tab moves focus and Escape closes a dialog, unlike a key event made by a page script.
/// </summary>
public static class BrowserKeys
{
    private static readonly FrozenDictionary<string, Key> Keys = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"] = new("Enter", "Enter", 13, "\r"),
        ["Tab"] = new("Tab", "Tab", 9, null),
        ["Escape"] = new("Escape", "Escape", 27, null),
        ["Backspace"] = new("Backspace", "Backspace", 8, null),
        ["Delete"] = new("Delete", "Delete", 46, null),
        ["Space"] = new(" ", "Space", 32, " "),
        ["ArrowUp"] = new("ArrowUp", "ArrowUp", 38, null),
        ["ArrowDown"] = new("ArrowDown", "ArrowDown", 40, null),
        ["ArrowLeft"] = new("ArrowLeft", "ArrowLeft", 37, null),
        ["ArrowRight"] = new("ArrowRight", "ArrowRight", 39, null),
        ["Home"] = new("Home", "Home", 36, null),
        ["End"] = new("End", "End", 35, null),
        ["PageUp"] = new("PageUp", "PageUp", 33, null),
        ["PageDown"] = new("PageDown", "PageDown", 34, null),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string Names { get; } = string.Join(", ", Keys.Values.Select(static key => key.Code));

    public static bool IsKnown(string name) => Keys.ContainsKey(name);

    /// <summary>The two events of a press: key down (with the typed character, if any), then key up.</summary>
    /// <exception cref="KeyNotFoundException">The key is not in the list (<see cref="IsKnown"/>).</exception>
    public static IReadOnlyList<string> Press(string name)
    {
        var key = Keys[name];
        var down = Event(key, key.Text is null ? "rawKeyDown" : "keyDown");
        if (key.Text is { } text)
        {
            down["text"] = text;
        }

        return [down.ToJsonString(), Event(key, "keyUp").ToJsonString()];
    }

    private static JsonObject Event(Key key, string type) => new()
    {
        ["type"] = type,
        ["key"] = key.Value,
        ["code"] = key.Code,
        ["windowsVirtualKeyCode"] = key.VirtualKey,
        ["nativeVirtualKeyCode"] = key.VirtualKey,
    };

    private sealed record Key(string Value, string Code, int VirtualKey, string? Text);
}
