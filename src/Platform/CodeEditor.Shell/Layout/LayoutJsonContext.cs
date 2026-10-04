using System.Text.Json.Serialization;

namespace CodeEditor.Shell.Layout;

/// <summary>Source-generated layout serialization: no reflection, fast at startup.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LayoutState))]
internal sealed partial class LayoutJsonContext : JsonSerializerContext;
