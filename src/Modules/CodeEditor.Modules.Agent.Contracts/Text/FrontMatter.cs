using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Resources;

namespace CodeEditor.Modules.Agent.Contracts.Text;

/// <summary>
/// YAML header of a Markdown file between <c>---</c> lines (agent.md, rules, tools). Supports a small subset:
/// <c>key: value</c>, lists (<c>[a, b]</c> or indented <c>- a</c> lines), one-level maps (indented <c>name: text</c>
/// lines) and <c>#</c> comments.
/// </summary>
public sealed class FrontMatter
{
    private const string Fence = "---";

    private readonly Dictionary<string, Entry> _entries;

    private FrontMatter(Dictionary<string, Entry> entries, string body, IReadOnlyList<string> errors)
    {
        _entries = entries;
        Body = body;
        Errors = errors;
    }

    /// <summary>Text after the header; the whole text when there is no header.</summary>
    public string Body { get; }

    /// <summary>Problems with line numbers, for the status bar and the log.</summary>
    public IReadOnlyList<string> Errors { get; }

    public IEnumerable<string> Keys => _entries.Keys;

    /// <remarks>O(n) over the text.</remarks>
    public static FrontMatter Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('﻿').Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd() != Fence)
        {
            return new FrontMatter([], text.Trim(), []);
        }

        var end = Array.FindIndex(lines, 1, line => line.TrimEnd() == Fence);
        if (end < 0)
        {
            return new FrontMatter([], text.Trim(), [Strings.FrontMatterNotClosed]);
        }

        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        Entry? current = null;
        for (var i = 1; i < end; i++)
        {
            current = ParseLine(lines[i], i + 1, entries, current, errors);
        }

        return new FrontMatter(entries, string.Join('\n', lines[(end + 1)..]).Trim(), errors);
    }

    /// <summary>Scalar value; <c>null</c> — no such key or it is a list.</summary>
    public string? Text(string key) => _entries.TryGetValue(key, out var entry) ? entry.Scalar : null;

    /// <summary>List value; a scalar becomes a list split by commas.</summary>
    public IReadOnlyList<string> List(string key) =>
        !_entries.TryGetValue(key, out var entry) ? []
            : entry.Scalar is { } scalar ? SplitInline(scalar)
            : entry.Items;

    /// <summary>Map value in file order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Map(string key) => _entries.TryGetValue(key, out var entry) ? entry.Pairs : [];

    private static Entry? ParseLine(string line, int number, Dictionary<string, Entry> entries, Entry? current, List<string> errors)
    {
        var content = StripComment(line);
        if (content.Trim().Length == 0)
        {
            return current;
        }

        if (char.IsWhiteSpace(content[0]))
        {
            if (current is null || !current.AddChild(content.Trim()))
            {
                errors.Add(Format(Strings.FrontMatterBadLine, number));
            }

            return current;
        }

        var colon = content.IndexOf(':', StringComparison.Ordinal);
        var key = colon > 0 ? content[..colon].Trim() : string.Empty;
        if (key.Length == 0)
        {
            errors.Add(Format(Strings.FrontMatterBadLine, number));
            return null;
        }

        if (entries.ContainsKey(key))
        {
            errors.Add(string.Format(CultureInfo.CurrentCulture, Strings.FrontMatterDuplicateKey, number, key));
            return null;
        }

        var entry = Entry.Create(content[(colon + 1)..].Trim());
        entries[key] = entry;
        return entry;
    }

    // "#" starts a comment unless it is inside quotes or glued to a word ("C#").
    private static string StripComment(string line)
    {
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is { } open)
            {
                quote = c == open ? null : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
            {
                return line[..i];
            }
        }

        return line;
    }

    private static IReadOnlyList<string> SplitInline(string value)
    {
        var inner = value.StartsWith('[') && value.EndsWith(']') ? value[1..^1] : value;
        var items = new List<string>();
        var start = 0;
        char? quote = null;
        for (var i = 0; i <= inner.Length; i++)
        {
            if (i < inner.Length && inner[i] is '"' or '\'')
            {
                quote = quote == inner[i] ? null : quote ?? inner[i];
            }
            else if (i == inner.Length || (inner[i] == ',' && quote is null))
            {
                if (Unquote(inner[start..i].Trim()) is { Length: > 0 } item)
                {
                    items.Add(item);
                }

                start = i + 1;
            }
        }

        return items;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0] ? value[1..^1] : value;

    private static string Format(string format, int line) => string.Format(CultureInfo.CurrentCulture, format, line);

    private sealed class Entry
    {
        public string? Scalar { get; private init; }

        public List<string> Items { get; } = [];

        public List<KeyValuePair<string, string>> Pairs { get; } = [];

        public static Entry Create(string value) => value.Length == 0
            ? new Entry()
            : value.StartsWith('[') ? FromList(SplitInline(value)) : new Entry { Scalar = Unquote(value) };

        // Indented line under a key: "- item" or "name: text".
        public bool AddChild(string line)
        {
            if (Scalar is not null)
            {
                return false;
            }

            if (line == "-" || line.StartsWith("- ", StringComparison.Ordinal))
            {
                Items.Add(Unquote(line[1..].Trim()));
                return true;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return false;
            }

            Pairs.Add(new(Unquote(line[..colon].Trim()), Unquote(line[(colon + 1)..].Trim())));
            return true;
        }

        private static Entry FromList(IReadOnlyList<string> items)
        {
            var entry = new Entry();
            entry.Items.AddRange(items);
            return entry;
        }
    }
}
