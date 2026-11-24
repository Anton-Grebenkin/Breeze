using System.Text.Json;

namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>A tool call in a probe reply.</summary>
/// <param name="Arguments">The arguments as the model wrote them: JSON text.</param>
internal sealed record ModelProbeCall(string Name, string Arguments)
{
    /// <summary>The arguments as JSON; <c>null</c> when the model wrote something else.</summary>
    public JsonDocument? Parse()
    {
        try
        {
            var document = JsonDocument.Parse(Arguments);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
