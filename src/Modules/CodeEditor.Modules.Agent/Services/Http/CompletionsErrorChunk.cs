using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// A failure mid-stream in OpenRouter's Chat Completions format: a chunk with <c>finish_reason: "error"</c> (sometimes
/// with an <c>error</c> object), after which the stream ends. The OpenAI library does not know this value and fails with
/// "Unknown ChatFinishReason value"; instead the turn gets a <see cref="ModelStreamException"/> with the service's text,
/// and <see cref="StreamFailureChatClient"/> may retry.
/// </summary>
internal static class CompletionsErrorChunk
{
    private const string DataPrefix = "data: ";
    private const string ErrorFinish = "\"finish_reason\":\"error\"";

    /// <exception cref="ModelStreamException">The stream line is a failure chunk.</exception>
    public static void ThrowIfError(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.StartsWith(DataPrefix, StringComparison.Ordinal) && line.Contains(ErrorFinish, StringComparison.Ordinal))
        {
            throw new ModelStreamException(Text(line));
        }
    }

    // Text and code from the error object if the service sent one; otherwise the finish reason itself.
    private static string Text(string line)
    {
        try
        {
            if (JsonNode.Parse(line[DataPrefix.Length..])?["error"] is JsonObject error
                && (string?)error["message"] is { Length: > 0 } message)
            {
                return error["code"] is { } code ? $"{message} ({code.ToJsonString().Trim('"')})" : message;
            }
        }
        catch (JsonException)
        {
            // The library parses the line too; the finish reason is enough here.
        }

        return "finish_reason: error";
    }
}
