using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Export;

namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// Calls into the renderer page (<c>renderer.js</c> in <c>Diagrams.Wpf</c>) over the DevTools protocol:
/// <c>Runtime.evaluate</c> awaits the promise and returns the whole value. The page answers <c>{ok: true, …}</c>, a
/// diagram error <c>{ok: false, message, line}</c>, or <c>{ok: false, unavailable: true}</c> if Mermaid did not load.
/// The diagram text enters the script only as a JSON string, so no code can be injected. Error lines are mapped to the
/// user's text lines (<see cref="PreparedMermaid.TextLine"/>).
/// </summary>
public static class MermaidProtocol
{
    public const string Method = "Runtime.evaluate";

    /// <summary>Waits for the page to load Mermaid.</summary>
    public static string Ready { get; } = Evaluate("diagrams.ready()");

    public static string Check(PreparedMermaid source) => Evaluate($"diagrams.check({Json(source.Text)})");

    public static string Svg(PreparedMermaid source, DiagramTheme theme) =>
        Evaluate($"diagrams.svg({Json(source.Text)}, {Json(ThemeName(theme))})");

    public static string Png(PreparedMermaid source, DiagramTheme theme, DiagramPngSize size) =>
        Evaluate(string.Create(CultureInfo.InvariantCulture, $"diagrams.png({Json(source.Text)}, {Json(ThemeName(theme))}, {size.Scale}, {size.MaxSide})"));

    /// <summary>The Mermaid theme name.</summary>
    public static string ThemeName(DiagramTheme theme) => theme == DiagramTheme.Dark ? "dark" : "default";

    /// <summary>Reads the answer to <see cref="Ready"/>.</summary>
    /// <exception cref="DiagramRendererException">The script failed, there was no answer or Mermaid did not load.</exception>
    public static void ReadReady(string response)
    {
        using var document = Parse(response);
        Value(document.RootElement);
    }

    /// <summary>The string value of the answer: <c>type</c> for a check or <c>svg</c> for a render.</summary>
    /// <exception cref="DiagramRendererException">The script failed, there was no answer or Mermaid did not load.</exception>
    public static DiagramResult<string> ReadText(string response, string property, PreparedMermaid source)
    {
        using var document = Parse(response);
        var value = Value(document.RootElement);
        return Failure<string>(value, source) ?? DiagramResult<string>.Success(Text(value, property));
    }

    /// <summary>The PNG from the answer: base64 in <c>png</c>.</summary>
    /// <exception cref="DiagramRendererException">
    /// The script failed, there was no answer, Mermaid did not load or the image is corrupt.
    /// </exception>
    public static DiagramResult<byte[]> ReadPng(string response, PreparedMermaid source)
    {
        using var document = Parse(response);
        var value = Value(document.RootElement);
        if (Failure<byte[]>(value, source) is { } failure)
        {
            return failure;
        }

        try
        {
            return DiagramResult<byte[]>.Success(Convert.FromBase64String(Text(value, "png")));
        }
        catch (FormatException exception)
        {
            throw new DiagramRendererException(Strings.RendererNoAnswer, exception);
        }
    }

    private static string Evaluate(string expression) =>
        new JsonObject { ["expression"] = expression, ["awaitPromise"] = true, ["returnByValue"] = true }.ToJsonString();

    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static JsonDocument Parse(string response)
    {
        try
        {
            return JsonDocument.Parse(response);
        }
        catch (JsonException exception)
        {
            throw new DiagramRendererException(Strings.RendererNoAnswer, exception);
        }
    }

    private static JsonElement Value(JsonElement root)
    {
        if (root.TryGetProperty("exceptionDetails", out var details))
        {
            throw new DiagramRendererException(Format(Strings.RendererScriptFailed, ExceptionText(details)));
        }

        if (!root.TryGetProperty("result", out var result) || !result.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new DiagramRendererException(Strings.RendererNoAnswer);
        }

        if (value.TryGetProperty("unavailable", out var unavailable) && unavailable.ValueKind == JsonValueKind.True)
        {
            throw new DiagramRendererException(Format(Strings.MermaidUnavailable, Text(value, "message")));
        }

        return value;
    }

    private static DiagramResult<T>? Failure<T>(JsonElement value, PreparedMermaid source)
        where T : class
    {
        if (value.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        int? line = value.TryGetProperty("line", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var parsed)
            ? parsed
            : null;
        return DiagramResult<T>.Failure(new DiagramError(Text(value, "message"), source.TextLine(line)));
    }

    private static string ExceptionText(JsonElement details) =>
        details.TryGetProperty("exception", out var exception) && Text(exception, "description") is { Length: > 0 } description
            ? description
            : Text(details, "text");

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
