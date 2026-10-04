using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Detects a tool call written as text instead of a function call; models occasionally do this, and the feed would show
/// it as the answer. Recognizes the native formats of Qwen (<c>&lt;function=…&gt;</c>, <c>&lt;tool_call&gt;</c>),
/// Anthropic (<c>&lt;invoke name=…&gt;</c>), Mistral (<c>[TOOL_CALLS]</c>) and JSON with <c>name</c> and
/// <c>arguments</c> fields.
/// </summary>
public static partial class TextToolCall
{
    public static bool IsIn(string? report) => !string.IsNullOrEmpty(report) && Pattern().IsMatch(report);

    [GeneratedRegex("""<function=[\w.-]+>|</?tool_call>|<invoke name=|\[TOOL_CALLS\]|\{\s*"name"\s*:\s*"[\w.-]+"\s*,\s*"(?:arguments|parameters)"\s*:""", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
