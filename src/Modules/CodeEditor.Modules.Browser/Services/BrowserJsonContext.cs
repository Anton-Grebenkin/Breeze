using System.Text.Json.Serialization;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>JSON of the browser's own files (history, tabs), without reflection.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(List<BrowserVisit>))]
[JsonSerializable(typeof(List<BrowserTabRecord>))]
internal sealed partial class BrowserJsonContext : JsonSerializerContext;
