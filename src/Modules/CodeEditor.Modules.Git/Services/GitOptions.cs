using CodeEditor.Modules.Git.Services.Agent;

namespace CodeEditor.Modules.Git.Services;

/// <summary>Git module settings: the <c>git</c> section of settings.json.</summary>
public sealed class GitOptions
{
    public const string Section = "git";

    /// <summary>
    /// Changes the agent makes without a card ("Always allow"): commit, create_branch, switch, stash, stash_pop. Push and
    /// restore never go here (<see cref="GitApprovals"/>).
    /// </summary>
    public List<string> AlwaysAllow { get; set; } = [];
}
