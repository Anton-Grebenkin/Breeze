using System.Text.Json.Serialization;

namespace CodeEditor.Shell.Workspace;

/// <summary>Source-generated serialization for small shell files.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ShellJsonContext : JsonSerializerContext;
