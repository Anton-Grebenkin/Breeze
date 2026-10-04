using System.Text.Json.Serialization;

namespace CodeEditor.Shell.Instances;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WindowEntry))]
[JsonSerializable(typeof(InstanceRequest))]
internal sealed partial class InstanceJsonContext : JsonSerializerContext;
