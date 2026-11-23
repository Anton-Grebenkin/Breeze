using System.Globalization;
using System.Text;
using CodeEditor.Modules.Browser.Resources;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// Browser tabs as lines of the agent's context block. A tab is named by site and path only: the query string and the
/// fragment may carry tokens. The title is written by the page, so it is quoted as data, cut short and stripped of
/// line breaks and angle brackets, which could imitate the block's own markup.
/// </summary>
public static class BrowserTabLines
{
    public const int MaxTabs = 10;
    public const int MaxAddress = 120;
    public const int MaxTitle = 80;

    public static IReadOnlyList<string> Describe(IReadOnlyList<BrowserTabInfo> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        if (tabs.Count == 0)
        {
            return [];
        }

        var lines = new List<string>(Math.Min(tabs.Count, MaxTabs) + 2) { Strings.ContextTabs };
        lines.AddRange(tabs.Take(MaxTabs).Select(static tab => Line(tab, Address(tab.Url), tab.IsAgent ? Strings.ContextTabAgent : string.Empty)));
        if (tabs.Count > MaxTabs)
        {
            lines.Add(Format(Strings.ContextTabsMore, tabs.Count - MaxTabs));
        }

        return lines;
    }

    /// <summary>
    /// Every tab with its full address, for the <c>tabs</c> action: the agent asked, and a snapshot shows the address
    /// anyway.
    /// </summary>
    /// <param name="current">The id of the agent's working tab.</param>
    public static IReadOnlyList<string> List(IReadOnlyList<BrowserTabInfo> tabs, string? current)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        return [Strings.TabsList, .. tabs.Select(tab => Line(tab, tab.Url.AbsoluteUri, tab.Id == current ? Strings.TabCurrent : tab.IsAgent ? Strings.TabYours : string.Empty))];
    }

    /// <summary>"gitlab.example.com/group/project": the site and the path, no query string or fragment.</summary>
    public static string Address(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return Cut(url.Authority + url.AbsolutePath.TrimEnd('/'), MaxAddress);
    }

    private static string Line(BrowserTabInfo tab, string address, string owner)
    {
        var line = new StringBuilder("- ").Append(tab.Id).Append(": ").Append(address).Append(tab.IsActive ? Strings.ContextTabActive : string.Empty).Append(owner);

        return Clean(tab.Title) is { Length: > 0 } title ? line.Append(Format(Strings.ContextTabTitle, title)).ToString() : line.ToString();
    }

    private static string Clean(string title)
    {
        var text = new StringBuilder(title.Length);
        foreach (var symbol in title)
        {
            text.Append(symbol switch
            {
                '<' => '(',
                '>' => ')',
                '"' => '\'',
                _ when char.IsControl(symbol) || char.IsWhiteSpace(symbol) => ' ',
                _ => symbol,
            });
        }

        return Cut(string.Join(' ', text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)), MaxTitle);
    }

    private static string Cut(string text, int length) => text.Length <= length ? text : string.Concat(text.AsSpan(0, length), "…");

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
