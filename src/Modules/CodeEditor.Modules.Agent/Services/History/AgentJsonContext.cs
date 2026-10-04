using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>Chat files and their index without reflection; non-ASCII text is written as is, so history stays human-readable.</summary>
[JsonSerializable(typeof(ChatTranscript))]
[JsonSerializable(typeof(List<ChatSummary>))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AgentJsonContext : JsonSerializerContext
{
    public static AgentJsonContext Readable { get; } = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    });
}
