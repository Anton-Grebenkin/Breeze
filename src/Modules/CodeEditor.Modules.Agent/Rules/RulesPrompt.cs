using System.Globalization;
using System.Text;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>Section of the system prompt with the folder rules: texts, enforced settings and the index of rules for some files.</summary>
public static class RulesPrompt
{
    public static string Build(ProjectRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.IsEmpty)
        {
            return string.Empty;
        }

        var text = new StringBuilder("## Project rules\nFollow them: they override the general style rules above. Rules of the folder override personal rules.\n");
        foreach (var source in rules.Sources)
        {
            var tag = source.IsPersonal ? "personal_rules" : "project_rules";
            text.Append(CultureInfo.InvariantCulture, $"<{tag} source=\"{source.Source}\">\n{source.Text}\n</{tag}>\n");
        }

        if (rules.ProtectedPatterns.Count > 0)
        {
            text.Append("Protected paths — the user approves every change there: ").AppendJoin(", ", rules.ProtectedPatterns).Append('\n');
        }

        if (rules.AskBefore.Count > 0)
        {
            text.Append("The user approves these actions every time, even when edits are accepted automatically: ")
                .AppendJoin(", ", rules.AskBefore.Order(StringComparer.Ordinal)).Append('\n');
        }

        if (rules.Scoped.Count > 0)
        {
            text.Append("Rules for some files — the full text comes with the first read of a matching file; follow it there:\n");
            foreach (var rule in rules.Scoped)
            {
                var description = rule.Description.Length > 0 ? ": " + rule.Description : string.Empty;
                text.Append(CultureInfo.InvariantCulture, $"- {rule.Source} ({string.Join(", ", rule.Applies)}){description}\n");
            }
        }

        return text.ToString().TrimEnd();
    }
}
