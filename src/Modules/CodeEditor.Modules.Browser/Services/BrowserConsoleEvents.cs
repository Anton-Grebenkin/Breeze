using System.Text.Json;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>
/// Turns DevTools protocol events into console messages for the model: <c>console.*</c> calls, unhandled page exceptions
/// and browser log entries (script failed to load, resource 404, CSP error).
/// </summary>
public static class BrowserConsoleEvents
{
    public const string ConsoleApiCalled = "Runtime.consoleAPICalled";
    public const string ExceptionThrown = "Runtime.exceptionThrown";
    public const string LogEntryAdded = "Log.entryAdded";

    /// <summary>Message length limit: stacks and huge objects are cut.</summary>
    private const int MaxText = 1_000;

    public static IReadOnlyList<string> Methods { get; } = [ConsoleApiCalled, ExceptionThrown, LogEntryAdded];

    /// <returns>The message; <c>null</c> for an event without text.</returns>
    public static BrowserConsoleMessage? Parse(string method, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var message = method switch
        {
            ConsoleApiCalled => new BrowserConsoleMessage(Text(root, "type") ?? "log", Arguments(root)),
            ExceptionThrown when root.TryGetProperty("exceptionDetails", out var details) => new BrowserConsoleMessage("exception", Exception(details)),
            LogEntryAdded when root.TryGetProperty("entry", out var entry) => new BrowserConsoleMessage(Text(entry, "level") ?? "log", WithUrl(Text(entry, "text"), Text(entry, "url"))),
            _ => null,
        };
        return message is { Text.Length: > 0 } ? message with { Text = Short(message.Text) } : null;
    }

    private static string Arguments(JsonElement root) =>
        root.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
            ? string.Join(' ', args.EnumerateArray().Select(Value).Where(value => value.Length > 0))
            : string.Empty;

    // Strings and numbers as is; objects by their description ("Object", "Error: …").
    private static string Value(JsonElement argument) =>
        argument.TryGetProperty("value", out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            : Text(argument, "description") ?? string.Empty;

    private static string Exception(JsonElement details) =>
        details.TryGetProperty("exception", out var exception) && Text(exception, "description") is { Length: > 0 } description
            ? description
            : WithUrl(Text(details, "text"), Text(details, "url"));

    private static string WithUrl(string? text, string? url) =>
        string.IsNullOrEmpty(url) ? text ?? string.Empty : $"{text} ({url})";

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Short(string text) => text.Length <= MaxText ? text : string.Concat(text.AsSpan(0, MaxText), "…");
}
