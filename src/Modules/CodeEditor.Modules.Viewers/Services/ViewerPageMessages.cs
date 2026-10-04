using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Viewers.Services;

/// <summary>
/// Messages between the tab and the <c>svg.js</c> and <c>media.js</c> pages (ADR 0037). The tab sends show (file
/// address, theme, zoom), zoom, fit and pause; the page replies with ready, drawing size, wheel zoom, recording metadata
/// and error (<see cref="Read"/>). Text reaches the page only as a JSON string.
/// </summary>
public static class ViewerPageMessages
{
    public const string Ready = "ready";
    public const string Size = "size";
    public const string Zoom = "zoom";
    public const string Metadata = "metadata";
    public const string Error = "error";

    public static string Fit { get; } = Message("fit").ToJsonString();

    public static string Pause { get; } = Message("pause").ToJsonString();

    public static string ZoomTo(double value)
    {
        var message = Message(Zoom);
        message["value"] = value;
        return message.ToJsonString();
    }

    /// <summary>Shows an SVG drawing.</summary>
    /// <param name="zoom">The zoom; <c>null</c> fits the window.</param>
    /// <param name="theme">Page colors from the theme: <c>scheme</c>, <c>background</c>, <c>foreground</c> and others.</param>
    public static string ShowPicture(Uri source, double? zoom, IReadOnlyDictionary<string, string> theme)
    {
        var message = Show(source, theme);
        message["zoom"] = zoom;
        return message.ToJsonString();
    }

    /// <summary>Shows audio or video: a player with controls, no autoplay.</summary>
    public static string ShowMedia(Uri source, bool video, IReadOnlyDictionary<string, string> theme)
    {
        var message = Show(source, theme);
        message["video"] = video;
        return message.ToJsonString();
    }

    /// <returns>The message, or <c>null</c> if the JSON is not a page message.</returns>
    public static ViewerPageMessage? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return new ViewerPageMessage(type.GetString()!)
            {
                Width = Number(root, "width"),
                Height = Number(root, "height"),
                Value = Number(root, "value"),
                Duration = Number(root, "duration"),
                Fit = root.TryGetProperty("fit", out var fit) && fit.ValueKind == JsonValueKind.True,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject Message(string type) => new() { ["type"] = type };

    private static JsonObject Show(Uri source, IReadOnlyDictionary<string, string> theme)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(theme);
        var colors = new JsonObject();
        foreach (var (name, value) in theme)
        {
            colors[name] = value;
        }

        var message = Message("show");
        message["src"] = source.AbsoluteUri;
        message["theme"] = colors;
        return message;
    }

    // Infinity and NaN (a stream's duration) never arrive: JSON cannot carry them, so the page sends null.
    private static double? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
