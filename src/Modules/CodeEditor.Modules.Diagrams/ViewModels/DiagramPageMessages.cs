using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Messages between the preview tab and the <c>preview.js</c> page (ADR 0035). The tab sends the whole state
/// (<see cref="Show"/>: diagrams, captions, placeholder, theme and zoom), the zoom (<see cref="Zoom"/>) and fit
/// (<see cref="Fit"/>); the page answers with readiness, wheel zoom and the + - 0 keys (<see cref="Read"/>).
/// </summary>
public static class DiagramPageMessages
{
    public const string Ready = "ready";
    public const string ZoomType = "zoom";
    public const string Key = "key";

    public const string ZoomInKey = "zoomIn";
    public const string ZoomOutKey = "zoomOut";
    public const string ZoomResetKey = "zoomReset";

    public static string Fit { get; } = new JsonObject { ["type"] = "fit" }.ToJsonString();

    /// <param name="palette">
    /// Page colors from the editor theme: <c>background</c>, <c>foreground</c>, <c>secondary</c>, <c>focus</c>.
    /// </param>
    public static string Show(DiagramPreviewViewModel preview, IReadOnlyDictionary<string, string> palette)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(palette);
        var items = new JsonArray();
        foreach (var item in preview.Diagrams)
        {
            items.Add(new JsonObject { ["caption"] = item.Caption, ["svg"] = item.Svg, ["stale"] = item.IsStale });
        }

        var theme = new JsonObject { ["scheme"] = preview.Theme == DiagramTheme.Dark ? "dark" : "light" };
        foreach (var (name, color) in palette)
        {
            theme[name] = color;
        }

        return new JsonObject
        {
            ["type"] = "show",
            ["items"] = items,
            ["placeholder"] = preview.Placeholder,
            ["theme"] = theme,
            ["zoom"] = preview.Zoom,
        }.ToJsonString();
    }

    public static string Zoom(double zoom) => new JsonObject { ["type"] = ZoomType, ["value"] = zoom }.ToJsonString();

    /// <returns>The message; <c>null</c> if it is not a page message.</returns>
    public static DiagramPageMessage? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            double? zoom = root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
            var key = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
            return new DiagramPageMessage(type.GetString()!, zoom, key);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
