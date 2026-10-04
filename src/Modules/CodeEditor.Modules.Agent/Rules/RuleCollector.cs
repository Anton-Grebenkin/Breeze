using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Text;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>Builds a <see cref="ProjectRuleSet"/> from rule files: parses headers, checks settings, applies the size limit.</summary>
internal sealed class RuleCollector
{
    private const string ProtectedKey = "protected";
    private const string AskBeforeKey = "ask_before";
    private const string AppliesKey = "applies";
    private const string DescriptionKey = "description";
    private const int MaxScopedCharacters = 8_000;

    private static readonly FrozenSet<string> AgentKeys = FrozenSet.ToFrozenSet([ProtectedKey, AskBeforeKey], StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> RuleKeys = FrozenSet.ToFrozenSet([AppliesKey, DescriptionKey], StringComparer.OrdinalIgnoreCase);

    private readonly List<ProjectRules> _sources = [];
    private readonly List<string> _protected = [];
    private readonly HashSet<string> _askBefore = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ScopedRule> _scoped = [];
    private readonly List<string> _problems = [];

    /// <summary>agent.md: settings from the header, rules from the text.</summary>
    public void AddAgentFile(string source, string text, bool personal)
    {
        var header = Parse(source, text, AgentKeys);
        _protected.AddRange(header.List(ProtectedKey));
        foreach (var category in header.List(AskBeforeKey))
        {
            if (AskBeforeCategories.All.Contains(category))
            {
                _askBefore.Add(category);
            }
            else
            {
                _problems.Add(Format(Strings.RulesUnknownCategory, source, category, string.Join(", ", AskBeforeCategories.All.Order(StringComparer.Ordinal))));
            }
        }

        AddText(source, header.Body, personal);
    }

    /// <summary>A rules file without settings: AGENTS.md, CLAUDE.md.</summary>
    public void AddText(string source, string text, bool personal = false)
    {
        // The same text twice (CLAUDE.md that repeats AGENTS.md) is sent once.
        if (text.Length > 0 && !_sources.Any(existing => existing.Text == text))
        {
            _sources.Add(new ProjectRules(source, text, personal));
        }
    }

    /// <summary>Rule for some files; without <c>applies</c> it is a general rule for the prompt.</summary>
    public void AddScopedRule(string source, string text)
    {
        var header = Parse(source, text, RuleKeys);
        var applies = header.List(AppliesKey);
        if (applies.Count == 0)
        {
            AddText(source, header.Body);
            return;
        }

        if (header.Body.Length > 0)
        {
            var body = header.Body.Length > MaxScopedCharacters ? header.Body[..MaxScopedCharacters] : header.Body;
            _scoped.Add(new ScopedRule(source, header.Text(DescriptionKey) ?? string.Empty, applies, body));
        }
    }

    /// <summary>Rules text over the limit is cut: it goes into every request, and long rules are followed worse.</summary>
    public ProjectRuleSet Build(int maxLines, int maxCharacters)
    {
        var sources = new List<ProjectRules>(_sources.Count);
        var lines = maxLines;
        var characters = maxCharacters;
        foreach (var source in _sources.OrderBy(source => source.IsPersonal))
        {
            if (lines <= 0 || characters <= 0)
            {
                _problems.Add(Format(Strings.RulesSkipped, source.Source, maxLines));
                continue;
            }

            var text = Limit(source.Text, ref lines, ref characters, maxLines);
            sources.Add(source with { Text = text });
        }

        return new ProjectRuleSet(sources, [.. _protected.Distinct(StringComparer.Ordinal)], _askBefore.ToFrozenSet(StringComparer.OrdinalIgnoreCase), _scoped, _problems);
    }

    private FrontMatter Parse(string source, string text, FrozenSet<string> keys)
    {
        var header = FrontMatter.Parse(text);
        _problems.AddRange(header.Errors.Select(error => $"{source}: {error}"));
        _problems.AddRange(header.Keys.Where(key => !keys.Contains(key)).Select(key => Format(Strings.RulesUnknownSetting, source, key, string.Join(", ", keys.Order(StringComparer.Ordinal)))));
        return header;
    }

    private static string Limit(string text, ref int lines, ref int characters, int maxLines)
    {
        var all = text.Split('\n');
        var kept = all.Length > lines ? string.Join('\n', all.Take(lines)) : text;
        if (kept.Length > characters)
        {
            kept = kept[..characters];
        }

        lines -= Math.Min(all.Length, lines);
        characters -= kept.Length;
        return kept.Length < text.Length ? kept + $"\n…(truncated: the rules are longer than {maxLines} lines)" : kept;
    }

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
