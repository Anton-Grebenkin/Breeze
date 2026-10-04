using System.Text;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Rewrites <c>&amp;&amp;</c> and <c>||</c> chains, which Windows PowerShell 5.1 lacks but models write all the time:
/// <c>a &amp;&amp; b</c> → <c>a; if ($?) { b }</c>, <c>a || b</c> → <c>a; if (-not $?) { b }</c>. Only top-level
/// operators outside quotes and brackets are handled; otherwise the command is unchanged. O(n) in the line length.
/// </summary>
public static class PowerShellChains
{
    public static string Rewrite(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var (parts, operators) = Split(command);
        return operators.Count == 0 ? command : Join(parts, operators, 0);
    }

    // Right-nested: "a && b || c" → "a; if ($?) { b; if (-not $?) { c } }".
    private static string Join(List<string> parts, List<string> operators, int index) =>
        index == operators.Count
            ? parts[index]
            : $"{parts[index]}; if ({(operators[index] == "&&" ? "$?" : "-not $?")}) {{ {Join(parts, operators, index + 1)} }}";

    private static (List<string> Parts, List<string> Operators) Split(string command)
    {
        var parts = new List<string>();
        var operators = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        char? quote = null;
        for (var index = 0; index < command.Length; index++)
        {
            var symbol = command[index];
            var next = index + 1 < command.Length ? command[index + 1] : '\0';
            if (quote is { } open)
            {
                quote = symbol == open ? null : quote;
            }
            else if (symbol is '\'' or '"')
            {
                quote = symbol;
            }
            else if (symbol is '(' or '{')
            {
                depth++;
            }
            else if (symbol is ')' or '}')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && (symbol, next) is ('&', '&') or ('|', '|'))
            {
                parts.Add(current.ToString().Trim());
                operators.Add(symbol == '&' ? "&&" : "||");
                current.Clear();
                index++;
                continue;
            }

            current.Append(symbol);
        }

        parts.Add(current.ToString().Trim());
        return (parts, operators);
    }
}
