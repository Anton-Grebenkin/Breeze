using System.Collections.Immutable;

namespace CodeEditor.Shell.Zoom;

/// <summary>
/// Zoom key gestures, as in VS Code: the main key with its Shift and numeric keypad variants. The interface zoom binds
/// them globally; a viewer with its own zoom binds the same gestures while its tab is active. The main key is last, so
/// it is the binding shown in menus and the palette (the last registered one).
/// </summary>
public static class ZoomKeys
{
    public static ImmutableArray<string> In { get; } = ["Ctrl+Shift+=", "Ctrl+Add", "Ctrl+="];

    public static ImmutableArray<string> Out { get; } = ["Ctrl+Shift+-", "Ctrl+Subtract", "Ctrl+-"];

    public static ImmutableArray<string> Reset { get; } = ["Ctrl+NumPad0", "Ctrl+0"];
}
