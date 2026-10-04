using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Search.Resources;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Search row in the agent feed (ADR 0010), e.g. "Search "Reserve" · 5 matches in 2 files". Totals are parsed from
/// <see cref="SearchOutput"/>: "line: text" match rows and file headers, or "path (count)" in files mode.
/// </summary>
public sealed class SearchToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Name != SearchAgentTools.SearchTextName)
        {
            return null;
        }

        var query = call.Text("query") ?? "?";
        return new AgentToolView(AgentToolIcon.Search, string.Format(CultureInfo.CurrentCulture, Strings.SearchTitle, query))
        {
            IsExploration = true,
            Detail = call.Result is { } result ? Summary(result) : null,
        };
    }

    private static string Summary(string result)
    {
        if (result.StartsWith(Strings.ToolNothingFound, StringComparison.Ordinal))
        {
            return Strings.NothingFound;
        }

        var lines = result.Split('\n').Where(line => line.Length > 0 && !line.StartsWith('…')).ToList();
        if (lines[0].StartsWith(CountHeaderPrefix(), StringComparison.Ordinal))
        {
            return lines[0].TrimEnd('.').ToLowerInvariant();
        }

        var counted = lines.Select(FileCount).Where(count => count >= 0).ToList();
        return counted.Count == lines.Count
            ? Found(counted.Sum(), counted.Count)
            : Found(lines.Count(IsMatch), lines.Count(line => !char.IsAsciiDigit(line[0])));
    }

    private static string Found(int matches, int files) =>
        string.Format(CultureInfo.CurrentCulture, Strings.CountInFiles, Plural.Format(matches, Strings.MatchForms), Plural.Format(files, Strings.InFileForms));

    // The count-mode header is recognized by the fixed start of its format string.
    private static string CountHeaderPrefix()
    {
        var format = Strings.ToolCountHeader;
        return format[..format.IndexOf('{', StringComparison.Ordinal)];
    }

    // A match row is "line: text"; a context row is "line- text".
    private static bool IsMatch(string line)
    {
        var colon = line.IndexOf(": ", StringComparison.Ordinal);
        return colon > 0 && int.TryParse(line.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    // Files mode row "path (count)"; otherwise -1.
    private static int FileCount(string line)
    {
        var open = line.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 && line.EndsWith(')')
            && int.TryParse(line.AsSpan(open + 2, line.Length - open - 3), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count
            : -1;
    }
}
