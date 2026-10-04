using System.Text;

namespace CodeEditor.Modules.Agent.Contracts.Text;

/// <summary>
/// Balances brackets in JSON a model wrote as a string: an extra or mismatched closing bracket is dropped and missing
/// ones are appended (models tend to close an edit array as <c>…"}}]</c>). Brackets inside JSON strings (with escapes)
/// are ignored. One pass, O(n).
/// </summary>
public static class JsonBracketRepair
{
    public static string Balance(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var closers = new Stack<char>();
        var output = new StringBuilder(json.Length + 4);
        var inString = false;
        var escaped = false;
        foreach (var ch in json)
        {
            if (inString)
            {
                inString = escaped || ch != '"';
                escaped = !escaped && ch == '\\';
                output.Append(ch);
            }
            else if (Accept(ch, closers, ref inString))
            {
                output.Append(ch);
            }
        }

        if (inString)
        {
            output.Append('"');
        }

        while (closers.Count > 0)
        {
            output.Append(closers.Pop());
        }

        return output.ToString();
    }

    /// <returns><c>false</c> for an extra closing bracket, which is not written.</returns>
    private static bool Accept(char ch, Stack<char> closers, ref bool inString)
    {
        switch (ch)
        {
            case '"':
                inString = true;
                return true;
            case '[':
                closers.Push(']');
                return true;
            case '{':
                closers.Push('}');
                return true;
            case ']' or '}':
                if (closers.Count == 0 || closers.Peek() != ch)
                {
                    return false;
                }

                closers.Pop();
                return true;
            default:
                return true;
        }
    }
}
