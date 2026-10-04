using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Target of a link in an agent reply: a web address or a project file with a line: <c>src/App.cs:42</c>,
/// <c>src/App.cs:42:7</c>, <c>src/App.cs#L42</c> (as in GitHub Markdown links).
/// </summary>
public sealed partial record ChatLink(Uri? Web, string? FilePath, int Line, int Column)
{
    public static ChatLink Parse(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var text = target.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
        {
            return new ChatLink(uri, null, 0, 0);
        }

        if (uri is { IsFile: true })
        {
            text = uri.LocalPath;
        }

        var match = LocationPattern().Match(text);
        return match.Success
            ? new ChatLink(null, match.Groups["path"].Value, Number(match.Groups["line"]), Number(match.Groups["column"]))
            : new ChatLink(null, text, 0, 0);
    }

    private static int Number(Group group) =>
        group.Success ? int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture) : 0;

    // A drive letter "C:" is part of the path, not a line: a line is only trailing digits.
    [GeneratedRegex(@"^(?<path>.+?)(?:#L(?<line>\d+)(?:-L?\d+)?|:(?<line>\d+)(?::(?<column>\d+))?(?:-\d+)?)$")]
    private static partial Regex LocationPattern();
}
