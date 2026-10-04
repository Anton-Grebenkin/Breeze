using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// A page screenshot for the model via DevTools (<c>Page.captureScreenshot</c>, ADR 0027, 0030): from the scroll
/// position, panel width, page height capped at <see cref="MaxHeight"/>. The browser panel is short, so the visible part
/// alone shows little, while a whole long page gets shrunk by the model into an unreadable strip. Sizes come from
/// <c>Page.getLayoutMetrics</c>.
/// </summary>
public static class BrowserScreenshot
{
    /// <summary>Screenshot height limit in CSS pixels.</summary>
    public const int MaxHeight = 2000;

    private const int JpegQuality = 80;

    /// <summary><c>Page.captureScreenshot</c> parameters; without metrics, the visible part is captured.</summary>
    public static string Request(string layoutMetrics, BrowserImageFormat format)
    {
        var request = new JsonObject { ["format"] = format == BrowserImageFormat.Jpeg ? "jpeg" : "png" };
        if (format == BrowserImageFormat.Jpeg)
        {
            request["quality"] = JpegQuality;
        }

        if (Clip(layoutMetrics) is { } clip)
        {
            request["clip"] = clip;
            request["captureBeyondViewport"] = true;
        }

        return request.ToJsonString();
    }

    /// <summary>The image from a <c>Page.captureScreenshot</c> response.</summary>
    /// <exception cref="BrowserException">The response has no image.</exception>
    public static byte[] Data(string response)
    {
        ArgumentNullException.ThrowIfNull(response);
        try
        {
            // Decoded straight from the parsed JSON, without an intermediate multi-megabyte base64 string.
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.String && data.TryGetBytesFromBase64(out var image)
                    ? image
                    : throw new BrowserException(Strings.ScreenshotFailed);
        }
        catch (JsonException exception)
        {
            throw new BrowserException(Strings.ScreenshotFailed, exception);
        }
    }

    private static JsonObject? Clip(string layoutMetrics)
    {
        JsonNode? metrics;
        try
        {
            metrics = JsonNode.Parse(layoutMetrics);
        }
        catch (JsonException)
        {
            return null;
        }

        var viewport = metrics?["cssLayoutViewport"];
        if (Number(viewport, "pageX") is not { } left || Number(viewport, "pageY") is not { } top
            || Number(viewport, "clientWidth") is not { } width || Number(viewport, "clientHeight") is not { } visible
            || Number(metrics?["cssContentSize"], "height") is not { } content)
        {
            return null;
        }

        var height = Math.Min(Math.Max(content - top, visible), MaxHeight);
        return new JsonObject { ["x"] = left, ["y"] = top, ["width"] = width, ["height"] = height, ["scale"] = 1 };
    }

    private static double? Number(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;
}
