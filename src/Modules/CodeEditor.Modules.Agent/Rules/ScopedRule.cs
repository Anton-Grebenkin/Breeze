using CodeEditor.Core.Files;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>
/// Rule from <c>.breeze/rules/*.md</c> for some files (<c>applies</c> masks): the prompt lists it, and its text comes
/// to the model with the first read or edit of a matching file.
/// </summary>
public sealed record ScopedRule(string Source, string Description, IReadOnlyList<string> Applies, string Text)
{
    public GlobFilter Filter { get; } = GlobFilter.Create(Applies);
}
