using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeEditor.Core.Settings;

/// <summary>
/// Reflection-free serialization of setting values. Non-ASCII text is written as is, not as <c>\uXXXX</c>,
/// because people edit <c>settings.json</c> by hand.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
    public static SettingsJsonContext Readable { get; } = new(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
