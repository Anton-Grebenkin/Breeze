using System.Text;
using System.Text.Json.Nodes;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Removes a stray "{}" before call arguments in a Chat Completions stream. With Claude reasoning, Provod sends an
/// argument chunk <c>{}</c> (the empty input from the start of the <c>tool_use</c> block) right after a call starts,
/// followed by the real arguments. The library concatenated <c>{}{"path":…}</c>, failed to parse it and the tool got a
/// call without arguments, looping the agent. Such a chunk is held back: if more arguments for that call arrive it is
/// dropped; if the call ends without them it is released before the next event. Real arguments cannot start with "{}",
/// so the fix is safe for any service. State covers one response stream.
/// </summary>
internal sealed class ToolArgumentsStreamFix
{
    private const string DataPrefix = "data: ";
    private const string EmptyArguments = "{}";

    // Calls that already had arguments, and held "{}" events by call index.
    private readonly HashSet<int> _started = [];
    private readonly Dictionary<int, string> _held = [];

    /// <returns>The stream line; empty if the event is held; several events are separated by a blank line.</returns>
    public string Rewrite(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
        {
            return line;
        }

        var deltas = line.Contains("\"tool_calls\"", StringComparison.Ordinal) ? ArgumentDeltas(line) : [];
        if (deltas is [{ Arguments: EmptyArguments, Alone: true } held] && _started.Add(held.Index))
        {
            _held[held.Index] = line;
            return string.Empty;
        }

        foreach (var delta in deltas)
        {
            if (delta.Arguments.Length > 0)
            {
                _started.Add(delta.Index);
                _held.Remove(delta.Index);
            }
        }

        return Release(line, deltas);
    }

    // Held "{}" of calls absent from this event go before it: those calls ended without arguments.
    private string Release(string line, ArgumentDelta[] deltas)
    {
        if (_held.Count == 0)
        {
            return line;
        }

        var finished = _held.Keys.Where(index => Array.TrueForAll(deltas, delta => delta.Index != index)).ToList();
        if (finished.Count == 0)
        {
            return line;
        }

        var text = new StringBuilder();
        foreach (var index in finished)
        {
            text.Append(_held[index]).Append("\n\n");
            _held.Remove(index);
        }

        return text.Append(line).ToString();
    }

    private static ArgumentDelta[] ArgumentDeltas(string line)
    {
        if (JsonNode.Parse(line[DataPrefix.Length..]) is not JsonObject { } chunk || chunk["choices"] is not JsonArray choices)
        {
            return [];
        }

        var deltas = choices.OfType<JsonObject>().Select(static choice => choice["delta"]).OfType<JsonObject>().ToList();
        var calls = deltas.SelectMany(static delta => delta["tool_calls"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var alone = deltas.Count == 1 && deltas[0].Count == 1 && calls.Count == 1;
        return [.. calls.Select(call => new ArgumentDelta(
            (int?)call["index"] ?? 0,
            (string?)call["function"]?["arguments"] ?? string.Empty,
            alone && call["id"] is null && call["function"]?["name"] is null))];
    }

    /// <param name="Alone">The event holds only this argument chunk, so it can be held back whole.</param>
    private readonly record struct ArgumentDelta(int Index, string Arguments, bool Alone);
}
