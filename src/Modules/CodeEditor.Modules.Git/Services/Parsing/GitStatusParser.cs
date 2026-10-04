using System.Collections.Immutable;
using System.Globalization;

namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>
/// Parses <c>git status --porcelain=v2 -z --branch</c>: NUL-separated records with unquoted, unescaped paths, so spaces,
/// non-ASCII characters and quotes in names arrive as is. Paths are relative to the repository root. One pass, O(n);
/// records are read as spans, only paths and header values become strings.
/// </summary>
/// <remarks>
/// Records: <c># branch.*</c> are branch headers; <c>1 XY …</c> is a changed file (X is the index, Y the working tree,
/// '.' means unchanged); <c>2 XY … R100 path</c> is a rename, followed by a record with the original path; <c>u XY …</c>
/// is a conflict; <c>? path</c> is untracked.
/// </remarks>
public static class GitStatusParser
{
    // Fields before the path: "1 XY sub mH mI mW hH hI path", "2 XY sub mH mI mW hH hI X100 path",
    // "u XY sub m1 m2 m3 mW h1 h2 h3 path".
    private const int OrdinaryFields = 8;
    private const int RenamedFields = 9;
    private const int UnmergedFields = 10;
    private const int MinTrackedLength = 4;
    private const string InitialCommit = "(initial)";
    private const string DetachedHead = "(detached)";

    public static GitStatus Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var head = GitHead.Empty;
        var changes = ImmutableArray.CreateBuilder<GitFileChange>();
        var records = output.AsSpan().Split('\0');
        while (records.MoveNext())
        {
            var record = output.AsSpan(records.Current);
            if (record.Length < 2)
            {
                continue;
            }

            switch (record[0])
            {
                case '#':
                    head = ReadHeader(head, record);
                    break;
                case '1':
                    AddTracked(changes, record, OrdinaryFields, originalPath: null);
                    break;
                case '2':
                    AddTracked(changes, record, RenamedFields, records.MoveNext() ? output[records.Current] : null);
                    break;
                case 'u':
                    AddPath(changes, GitChangeGroup.Merge, GitFileStatus.Conflict, PathAfter(record, UnmergedFields));
                    break;
                case '?':
                    AddPath(changes, GitChangeGroup.Changes, GitFileStatus.Untracked, record[2..]);
                    break;
                default:
                    // '!' marks ignored files, which the panel does not request.
                    break;
            }
        }

        return new GitStatus(head, changes.ToImmutable());
    }

    // A file changed both in the index and after it ("MM") lands in both groups.
    private static void AddTracked(
        ImmutableArray<GitFileChange>.Builder changes, ReadOnlySpan<char> record, int fields, string? originalPath)
    {
        var path = PathAfter(record, fields);
        if (record.Length < MinTrackedLength || path.IsEmpty)
        {
            return;
        }

        var text = path.ToString();
        if (StatusOf(record[2]) is { } staged)
        {
            changes.Add(new GitFileChange(GitChangeGroup.Staged, staged, text, originalPath));
        }

        if (StatusOf(record[3]) is { } unstaged)
        {
            changes.Add(new GitFileChange(GitChangeGroup.Changes, unstaged, text));
        }
    }

    private static void AddPath(
        ImmutableArray<GitFileChange>.Builder changes, GitChangeGroup group, GitFileStatus status, ReadOnlySpan<char> path)
    {
        if (!path.IsEmpty)
        {
            changes.Add(new GitFileChange(group, status, path.ToString()));
        }
    }

    private static GitFileStatus? StatusOf(char code) => code switch
    {
        'M' => GitFileStatus.Modified,
        'A' => GitFileStatus.Added,
        'D' => GitFileStatus.Deleted,
        'R' => GitFileStatus.Renamed,
        'C' => GitFileStatus.Copied,
        'T' => GitFileStatus.TypeChanged,
        _ => null,
    };

    // The path is the rest of the record after the given number of fields: it may contain spaces.
    private static ReadOnlySpan<char> PathAfter(ReadOnlySpan<char> record, int fields)
    {
        var start = 0;
        for (var field = 0; field < fields; field++)
        {
            var space = record[start..].IndexOf(' ');
            if (space < 0)
            {
                return [];
            }

            start += space + 1;
        }

        return record[start..];
    }

    // "# branch.oid <hash>|(initial)", "# branch.head <branch>|(detached)", "# branch.upstream <branch>",
    // "# branch.ab +1 -2".
    private static GitHead ReadHeader(GitHead head, ReadOnlySpan<char> record)
    {
        var header = record[2..];
        var space = header.IndexOf(' ');
        if (space < 0)
        {
            return head;
        }

        var value = header[(space + 1)..];
        return header[..space] switch
        {
            "branch.oid" => head with { Commit = value is InitialCommit ? null : value.ToString() },
            "branch.head" => head with { Branch = value is DetachedHead ? null : value.ToString() },
            "branch.upstream" => head with { Upstream = value.ToString() },
            "branch.ab" => ReadAheadBehind(head, value),
            _ => head,
        };
    }

    private static GitHead ReadAheadBehind(GitHead head, ReadOnlySpan<char> value)
    {
        var space = value.IndexOf(' ');
        return space >= 0 && TryCount(value[..space], '+', out var ahead) && TryCount(value[(space + 1)..], '-', out var behind)
            ? head with { Ahead = ahead, Behind = behind }
            : head;
    }

    private static bool TryCount(ReadOnlySpan<char> text, char sign, out int count) =>
        int.TryParse(text.TrimStart(sign), NumberStyles.None, CultureInfo.InvariantCulture, out count);
}
