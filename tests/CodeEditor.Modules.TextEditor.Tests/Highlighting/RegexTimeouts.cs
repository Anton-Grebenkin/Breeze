using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>
/// Sets a timeout on every regex of a highlighting definition (including nested and imported ones), so catastrophic
/// backtracking fails the test with an exception instead of hanging it.
/// </summary>
internal static class RegexTimeouts
{
    public static void Apply(IHighlightingDefinition definition, TimeSpan timeout)
    {
        var visited = new HashSet<HighlightingRuleSet>();
        var pending = new Stack<HighlightingRuleSet>([definition.MainRuleSet]);
        while (pending.TryPop(out var ruleSet))
        {
            if (!visited.Add(ruleSet))
            {
                continue;
            }

            foreach (var rule in ruleSet.Rules)
            {
                rule.Regex = WithTimeout(rule.Regex, timeout);
            }

            foreach (var span in ruleSet.Spans)
            {
                span.StartExpression = WithTimeout(span.StartExpression, timeout);
                span.EndExpression = WithTimeout(span.EndExpression, timeout);
                if (span.RuleSet is { } nested)
                {
                    pending.Push(nested);
                }
            }
        }
    }

    private static Regex WithTimeout(Regex regex, TimeSpan timeout) =>
        regex.MatchTimeout == timeout ? regex : new Regex(regex.ToString(), regex.Options, timeout);
}
