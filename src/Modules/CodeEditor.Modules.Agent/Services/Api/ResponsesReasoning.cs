using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Strips past reasoning from Responses API history when the service does not pass it encrypted
/// (<see cref="ServiceDialect.EncryptedReasoning"/>). Unencrypted reasoning refers to a response stored by the vendor,
/// but our conversation is stateless, so the service would not find it. The model loses earlier reasoning, but the
/// request goes through. Applied by <see cref="JsonBodyPolicy"/>; one pass, O(n).
/// </summary>
internal static class ResponsesReasoning
{
    public static bool Strip(JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body["input"] is JsonArray items && items.RemoveAll(static item => (string?)item?["type"] == "reasoning") > 0;
    }
}
