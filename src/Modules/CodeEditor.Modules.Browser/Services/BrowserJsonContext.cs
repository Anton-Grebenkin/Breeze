using System.Text.Json.Serialization;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>JSON of the browser's own files (history), without reflection.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(List<BrowserVisit>))]
internal sealed partial class BrowserJsonContext : JsonSerializerContext;
