using System.Text.Json.Serialization;
using CodeEditor.Modules.Browser.Services.Tabs;

namespace CodeEditor.Modules.Browser.Services;

/// <summary>JSON of the browser's own files (history, tabs, known sites), without reflection.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(List<BrowserVisit>))]
[JsonSerializable(typeof(List<BrowserTabRecord>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class BrowserJsonContext : JsonSerializerContext;
