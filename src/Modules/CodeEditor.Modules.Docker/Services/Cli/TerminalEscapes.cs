using System.Buffers;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// Strips terminal control sequences from logs of apps run with a terminal (<c>tty: true</c>): color and cursor (CSI),
/// window title and links (OSC), other control characters. Newline and tab stay.
/// </summary>
public static partial class TerminalEscapes
{
    // C0 control characters except tab and newline, plus DEL; ESC is among them.
    private static readonly SearchValues<char> Controls = SearchValues.Create(
        [.. Enumerable.Range(0, 0x20).Select(code => (char)code).Where(character => character is not ('\t' or '\n')), '\u007F']);

    public static string Strip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // A plain log line has no control characters: one vectorized scan, no regex and no new string.
        return text.AsSpan().ContainsAny(Controls) ? Escapes().Replace(text, string.Empty) : text;
    }

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*(?:\x07|\x1B\\)?|\x1B[@-_]?|[\x00-\x08\x0B-\x1F\x7F]")]
    private static partial Regex Escapes();
}
