using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Terminal.ViewModels;

/// <summary>
/// The JSON between the panel and its page (one WebView2, a terminal per session). To the page: <c>init</c>,
/// <c>open</c>, <c>output</c>, <c>show</c>, <c>close</c>, <c>clear</c>, <c>theme</c>, <c>paste</c>, <c>focus</c>. From
/// the page: <see cref="TerminalPageMessage"/>.
/// </summary>
public static class TerminalPageMessages
{
    public const string Ready = "ready";
    public const string Input = "input";
    public const string Resize = "resize";
    public const string Copy = "copy";
    public const string Paste = "paste";

    public static string Init(TerminalTheme theme, double fontSize) =>
        Message("init", new JsonObject { ["theme"] = Theme(theme), ["fontSize"] = fontSize });

    public static string Open(int id, string replay) => Message("open", new JsonObject { ["id"] = id, ["data"] = replay });

    public static string Output(int id, string data) => Message("output", new JsonObject { ["id"] = id, ["data"] = data });

    public static string Show(int id) => Message("show", new JsonObject { ["id"] = id });

    public static string Close(int id) => Message("close", new JsonObject { ["id"] = id });

    public static string Clear(int id) => Message("clear", new JsonObject { ["id"] = id });

    public static string ThemeChanged(TerminalTheme theme) => Message("theme", new JsonObject { ["theme"] = Theme(theme) });

    public static string PasteText(int id, string text) => Message(Paste, new JsonObject { ["id"] = id, ["text"] = text });

    public static string Focus() => Message("focus", []);

    /// <summary>Reads a message from the page; <c>null</c> for anything malformed.</summary>
    public static TerminalPageMessage? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var type) && type.GetString() is { } kind
                ? new TerminalPageMessage(kind, Number(root, "id"), Text(root, "data") ?? Text(root, "text"), Number(root, "columns"), Number(root, "rows"))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Message(string type, JsonObject body)
    {
        body["type"] = type;
        return body.ToJsonString();
    }

    private static JsonObject Theme(TerminalTheme theme) => new()
    {
        ["scheme"] = theme.Scheme,
        ["background"] = theme.Background,
        ["foreground"] = theme.Foreground,
        ["cursor"] = theme.Cursor,
        ["selectionBackground"] = theme.Selection,
    };

    private static int Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
