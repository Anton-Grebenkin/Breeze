using System.ClientModel;
using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Error text from the service response body: <c>{"error": {"message", "code"}}</c> (OpenAI format, kept by ProxyAPI),
/// <c>{"error": "…"}</c> or unwrapped <c>{"message", "code"}</c> (Provod's 403 "first top-up required"). The body is
/// readable only if <see cref="ErrorBodyPolicy"/> buffered it.
/// </summary>
internal static class ServiceErrorText
{
    /// <summary>How many body characters go to the log: the body may be an HTML page.</summary>
    private const int LoggedBodyLength = 500;

    /// <returns>"Text (code)", just the text or the code; <c>null</c> if there is no body or no error in it.</returns>
    public static string? Of(ClientResultException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.GetRawResponse() is not { } response)
        {
            return null;
        }

        try
        {
            using var body = JsonDocument.Parse(response.Content.ToMemory());
            var root = body.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) ? Describe(error) : Describe(root);
        }
        catch (Exception unreadable) when (unreadable is JsonException or InvalidOperationException)
        {
            // Not JSON, or not buffered (an unread stream throws InvalidOperationException).
            return null;
        }
    }

    /// <summary>The raw response body for the log, so non-OpenAI error formats are visible too; <c>null</c> if none.</summary>
    public static string? Body(ClientResultException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            var text = exception.GetRawResponse()?.Content.ToString().ReplaceLineEndings(" ").Trim();
            return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, LoggedBodyLength)];
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? Describe(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
        {
            return error.GetString();
        }

        if (error.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var message = Field(error, "message");
        var code = Field(error, "code");
        return (message, code) switch
        {
            (null, null) => null,
            (null, _) => code,
            (_, null) => message,
            _ => $"{message} ({code})",
        };
    }

    // The code may be a string or a number.
    private static string? Field(JsonElement error, string name) => error.TryGetProperty(name, out var value) switch
    {
        false => null,
        true when value.ValueKind == JsonValueKind.String => value.GetString() is { Length: > 0 } text ? text : null,
        true when value.ValueKind == JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };
}
