using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>Branch label as in VS Code: "main*" has changes, "↑2 ↓1" is ahead and behind.</summary>
public static class GitBranchLabel
{
    public static string Name(GitHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return head.Branch ?? (head.ShortCommit is { } commit ? Format(Strings.DetachedHead, commit) : string.Empty);
    }

    public static string Text(GitStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var head = status.Head;
        var dirty = status.Changes.IsEmpty ? string.Empty : "*";
        var ahead = head.Ahead > 0 ? Invariant($" ↑{head.Ahead}") : string.Empty;
        var behind = head.Behind > 0 ? Invariant($" ↓{head.Behind}") : string.Empty;
        return Name(head) + dirty + ahead + behind;
    }

    /// <summary>"Branch main — switch to another branch", plus a line with what sync will push and pull.</summary>
    public static string ToolTip(GitHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        var branch = Format(Strings.BranchToolTip, Name(head));
        var sync = head.Upstream is { } upstream
            ? string.Format(CultureInfo.CurrentCulture, Strings.SyncToolTip, upstream, head.Ahead, head.Behind)
            : head.Branch is null ? null : Strings.NoUpstreamToolTip;
        return sync is null ? branch : branch + Environment.NewLine + sync;
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
