using System.Text.Json.Serialization;

namespace CodeEditor.Shell.Session;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionState))]
internal sealed partial class SessionJsonContext : JsonSerializerContext;
