using System.Collections.Frozen;
using CodeEditor.Core.Files;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>
/// Everything the agent follows in the open folder: rules text for the prompt, agent.md settings the editor enforces
/// (<c>protected</c>, <c>ask_before</c>), rules for some files, and problems with the files.
/// </summary>
public sealed record ProjectRuleSet(
    IReadOnlyList<ProjectRules> Sources,
    IReadOnlyList<string> ProtectedPatterns,
    FrozenSet<string> AskBefore,
    IReadOnlyList<ScopedRule> Scoped,
    IReadOnlyList<string> Problems)
{
    public static ProjectRuleSet Empty { get; } = new([], [], FrozenSet<string>.Empty, [], []);

    public GlobFilter Protected { get; } = GlobFilter.Create(ProtectedPatterns);

    public bool IsEmpty => Sources.Count == 0 && ProtectedPatterns.Count == 0 && AskBefore.Count == 0 && Scoped.Count == 0;
}
