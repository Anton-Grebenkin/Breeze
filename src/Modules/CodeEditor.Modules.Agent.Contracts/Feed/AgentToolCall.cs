using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Contracts.Feed;

/// <summary>
/// A tool call for a feed row: name, arguments (JSON from the model, objects in tests) and result;
/// <see cref="Result"/> is <c>null</c> while the tool is still running.
/// </summary>
public sealed record AgentToolCall(string Name, IReadOnlyDictionary<string, object?> Arguments, string? Result = null)
{
    /// <summary>The tool has finished.</summary>
    public bool IsDone => Result is not null;

    /// <summary>A string argument; <c>null</c> if missing or empty.</summary>
    public string? Text(string name) => Arguments.GetValueOrDefault(name) switch
    {
        null => null,
        string text => text.Length == 0 ? null : text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() is { Length: > 0 } text ? text : null,
        JsonElement { ValueKind: JsonValueKind.Null } => null,
        var other => Convert.ToString(other, CultureInfo.InvariantCulture),
    };

    /// <summary>An integer argument; <c>null</c> if missing or not a number.</summary>
    public int? Number(string name) => Arguments.GetValueOrDefault(name) switch
    {
        int number => number,
        long number => (int)number,
        JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var number) => number,
        _ => null,
    };

    /// <summary>Items of an array argument (objects as <see cref="JsonElement"/>); empty if there is no array.</summary>
    public IReadOnlyList<JsonElement> Items(string name) => Arguments.GetValueOrDefault(name) switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } array => [.. array.EnumerateArray()],
        System.Collections.IEnumerable items and not string => [.. items.Cast<object?>().Select(item => JsonSerializer.SerializeToElement(item, AIJsonUtilities.DefaultOptions))],
        _ => [],
    };
}
