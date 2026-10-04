using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// A tool call as a chat line: <c>read_file(path: src/Program.cs)</c>. Lists and objects are abbreviated
/// (<c>edits: [2]</c>); the approval card shows their content.
/// </summary>
public static class ToolCallText
{
    private const int MaxArgumentLength = 80;

    public static string Describe(FunctionCallContent call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var arguments = call.Arguments is null
            ? string.Empty
            : string.Join(", ", call.Arguments.Select(static pair => $"{pair.Key}: {Format(pair.Value)}"));
        return $"{call.Name}({arguments})";
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        JsonElement { ValueKind: JsonValueKind.Array } array => $"[{array.GetArrayLength()}]",
        JsonElement { ValueKind: JsonValueKind.Object } => "{…}",
        JsonElement { ValueKind: JsonValueKind.String } text => Shorten(text.GetString()),
        System.Collections.ICollection collection => $"[{collection.Count}]",
        _ => Shorten(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static string Shorten(string? value) =>
        value is null ? "null" : value.Length <= MaxArgumentLength ? value : string.Concat(value.AsSpan(0, MaxArgumentLength), "…");
}
