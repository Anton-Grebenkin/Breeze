using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Watchdog;

/// <summary>
/// What a tool call works on, for spotting repeats: the file, project, page or command. Two edits of different files are
/// different work; two failed edits of the same file are a repeat even when the text differs.
/// </summary>
public static partial class ToolTarget
{
    private const int MaxLength = 120;

    private static readonly string[] Keys = ["path", "project", "url", "file", "id", "command"];

    public static string Of(IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (Text(arguments, "input") is { } patch && PatchFile().Match(patch) is { Success: true } file)
        {
            return file.Groups["path"].Value.Trim();
        }

        if (arguments.TryGetValue("edits", out var edits) && FirstEditPath(edits) is { } edited)
        {
            return edited;
        }

        foreach (var key in Keys)
        {
            if (Text(arguments, key) is { Length: > 0 } value)
            {
                return value.Length <= MaxLength ? value : value[..MaxLength];
            }
        }

        return string.Empty;
    }

    private static string? Text(IReadOnlyDictionary<string, object?> arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
            int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        } : null;

    private static string? FirstEditPath(object? edits) => edits switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } array when array.GetArrayLength() > 0
            && array[0].ValueKind == JsonValueKind.Object && array[0].TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String
            => path.GetString(),
        _ => null,
    };

    [GeneratedRegex(@"^\*\*\* (?:Update|Add|Delete) File: (?<path>.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PatchFile();
}
