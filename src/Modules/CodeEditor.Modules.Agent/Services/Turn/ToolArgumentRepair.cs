using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts.Text;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Fixes arguments that models send off-schema but unambiguously: some models pass the <c>edits</c> array as a JSON
/// string, binding fails, and the model works around the tool (deleting and recreating the file). A string is parsed
/// when the parameter's schema is an array or object and the string is valid JSON of that kind. Anything else is left
/// alone; the wrapper explains the error.
/// </summary>
internal static class ToolArgumentRepair
{
    /// <returns>Names of the repaired parameters, for the log.</returns>
    public static IReadOnlyList<string> Repair(AIFunctionArguments arguments, JsonElement schema)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var repaired = new List<string>();
        foreach (var (name, value) in arguments.ToList())
        {
            if (Text(value) is { } text
                && properties.TryGetProperty(name, out var property)
                && ExpectedKind(property) is { } expected
                && Parse(text) is { } parsed
                && parsed.ValueKind == expected)
            {
                arguments[name] = parsed;
                repaired.Add(name);
            }
        }

        return repaired;
    }

    private static string? Text(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    // "type" is a string, or an array like ["array", "null"] for optional parameters.
    private static JsonValueKind? ExpectedKind(JsonElement property)
    {
        if (!property.TryGetProperty("type", out var type))
        {
            return null;
        }

        var names = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(static item => item.GetString()) : [type.GetString()];
        return names.Contains("array") ? JsonValueKind.Array : names.Contains("object") ? JsonValueKind.Object : null;
    }

    private static JsonElement? Parse(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed[0] is not ('[' or '{'))
        {
            return null;
        }

        return TryParse(trimmed) ?? TryParse(JsonBracketRepair.Balance(trimmed));
    }

    private static JsonElement? TryParse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
