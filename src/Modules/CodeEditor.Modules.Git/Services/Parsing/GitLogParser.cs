using System.Globalization;

namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>
/// Parses history and commit files. History is <c>git log -z</c> with fields separated by U+001F (<see cref="Format"/>);
/// the message body may span lines, so commits are NUL-separated. Files are <c>git show --name-status -z</c>: a status
/// letter ("R100" for a rename) and the path; a rename has the old and new path. One pass, O(n).
/// </summary>
public static class GitLogParser
{
    /// <summary>Commit fields for <c>--format</c>: hash, short hash, author, ISO 8601 date, refs, subject, body.</summary>
    public const string Format = "--format=%H%x1f%h%x1f%an%x1f%aI%x1f%D%x1f%s%x1f%b";

    private const char FieldSeparator = '\u001f';
    private const int FieldCount = 7;

    public static IReadOnlyList<GitCommitInfo> ParseLog(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var commits = new List<GitCommitInfo>();
        foreach (var record in output.Split('\0'))
        {
            var fields = record.Split(FieldSeparator, FieldCount);

            // The hash follows the last newline: a git warning from stderr may precede it.
            var hash = fields[0][(fields[0].LastIndexOf('\n') + 1)..];
            if (fields.Length == FieldCount && hash.Length > 0)
            {
                commits.Add(new GitCommitInfo(hash, fields[1], fields[2], ParseDate(fields[3]), fields[4], fields[5], fields[6].Trim()));
            }
        }

        return commits;
    }

    public static IReadOnlyList<GitCommitFile> ParseFiles(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var files = new List<GitCommitFile>();
        var tokens = output.Split('\0');
        for (var i = 0; i + 1 < tokens.Length; i++)
        {
            var code = tokens[i].Trim('\n');
            if (code.Length == 0)
            {
                continue;
            }

            var status = StatusOf(code[0]);
            var renamed = (status is GitFileStatus.Renamed or GitFileStatus.Copied) && i + 2 < tokens.Length;
            files.Add(renamed ? new GitCommitFile(status, tokens[i + 2], tokens[i + 1]) : new GitCommitFile(status, tokens[i + 1]));
            i += renamed ? 2 : 1;
        }

        return files;
    }

    private static GitFileStatus StatusOf(char code) => code switch
    {
        'A' => GitFileStatus.Added,
        'D' => GitFileStatus.Deleted,
        'R' => GitFileStatus.Renamed,
        'C' => GitFileStatus.Copied,
        'T' => GitFileStatus.TypeChanged,
        'U' => GitFileStatus.Conflict,
        _ => GitFileStatus.Modified,
    };

    private static DateTimeOffset ParseDate(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateTimeOffset.MinValue;
}
