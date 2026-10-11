using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>The JSON a hook reads on standard input, with Claude Code's field names so its hooks carry over.</summary>
public static class HookInputs
{
    /// <summary>A tool result or answer beyond this is cut: a hook needs the gist, not a 100 KB log.</summary>
    public const int MaxText = 20_000;

    public static JsonObject Tool(string tool, IEnumerable<KeyValuePair<string, object?>> arguments) => new()
    {
        ["tool_name"] = tool,
        ["tool_input"] = JsonSerializer.SerializeToNode(arguments.ToDictionary(), AIJsonUtilities.DefaultOptions),
    };

    public static JsonObject ToolResult(string tool, IEnumerable<KeyValuePair<string, object?>> arguments, string result, bool failed)
    {
        var input = Tool(tool, arguments);
        input["tool_response"] = Short(result);
        input["failed"] = failed;
        return input;
    }

    public static JsonObject Prompt(string text) => new() { ["prompt"] = text };

    /// <param name="active">A Stop hook already made the agent go on in this turn: a hook may let it finish now.</param>
    public static JsonObject Stop(string answer, bool active) => new() { ["last_answer"] = Short(answer), ["stop_hook_active"] = active };

    private static string Short(string text) => text.Length <= MaxText ? text : string.Concat(text.AsSpan(0, MaxText), "…");
}
