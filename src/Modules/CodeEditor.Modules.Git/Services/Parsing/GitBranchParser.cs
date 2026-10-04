namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>
/// Parses tab-separated <c>git for-each-ref</c> output (<see cref="Format"/>): branch names cannot contain tabs, and the
/// commit subject is the last field. <c>origin/HEAD</c> is skipped. O(n).
/// </summary>
public static class GitBranchParser
{
    /// <summary>Current marker ("*"), full ref name, short hash, upstream, commit subject.</summary>
    public const string Format = "--format=%(HEAD)%09%(refname)%09%(objectname:short)%09%(upstream:short)%09%(contents:subject)";

    private const string LocalPrefix = "refs/heads/";
    private const string RemotePrefix = "refs/remotes/";
    private const string RemoteHead = "/HEAD";
    private const int FieldCount = 5;

    public static IReadOnlyList<GitBranch> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var branches = new List<GitBranch>();
        foreach (var line in lines)
        {
            var fields = line.Split('\t', FieldCount);
            if (fields.Length == FieldCount && ToBranch(fields) is { } branch)
            {
                branches.Add(branch);
            }
        }

        return branches;
    }

    private static GitBranch? ToBranch(string[] fields)
    {
        var reference = fields[1];
        var isRemote = reference.StartsWith(RemotePrefix, StringComparison.Ordinal);
        if ((!isRemote && !reference.StartsWith(LocalPrefix, StringComparison.Ordinal))
            || (isRemote && reference.EndsWith(RemoteHead, StringComparison.Ordinal)))
        {
            return null;
        }

        var name = reference[(isRemote ? RemotePrefix.Length : LocalPrefix.Length)..];
        return new GitBranch(name, isRemote, fields[2], fields[4])
        {
            IsCurrent = fields[0] == "*",
            Upstream = fields[3].Length > 0 ? fields[3] : null,
        };
    }
}
