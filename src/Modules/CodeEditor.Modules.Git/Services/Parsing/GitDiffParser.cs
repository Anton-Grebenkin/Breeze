using System.Globalization;
using System.Text;
using CodeEditor.Modules.Git.Resources;

namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>
/// Parses <c>git diff</c> and <c>git show</c> output into display rows: file headers, hunk headers
/// (<c>@@ -12,7 +12,8 @@</c>), lines with old and new numbers, and notes (new file, rename, binary). A combined merge
/// diff (<c>@@@</c>) gets new-version numbers only. Lines outside the format (git warnings from stderr) are skipped.
/// One pass, O(n).
/// </summary>
public static class GitDiffParser
{
    public static IReadOnlyList<GitDiffRow> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var reader = new Reader();
        foreach (var line in lines)
        {
            reader.Read(line);
        }

        return reader.Rows;
    }

    private sealed class Reader
    {
        private const string GitHeader = "diff --git ";
        private const string CombinedHeader = "diff --cc ";
        private const string HunkMark = "@@";
        private const string DevNull = "/dev/null";

        // "a/" and "b/": panel commands set them explicitly, overriding the user's diff.noprefix and diff.mnemonicPrefix.
        private const int PrefixLength = 2;

        private int _fileRow = -1;
        private string? _oldPath;
        private string? _renameFrom;
        private string? _oldMode;
        private int _oldLine;
        private int _newLine;
        private int _oldLeft;
        private int _newLeft;

        // 0: outside a hunk; 1: a regular hunk; 2+: a combined merge diff with that many marker columns per line.
        private int _parents;

        public List<GitDiffRow> Rows { get; } = [];

        public void Read(string line)
        {
            if (TryStartFile(line) || TryStartHunk(line))
            {
                return;
            }

            if (_parents == 0)
            {
                ReadMeta(line);
            }
            else if (_parents == 1)
            {
                ReadHunkLine(line);
            }
            else
            {
                ReadCombinedLine(line);
            }
        }

        private bool TryStartFile(string line)
        {
            var path = line.StartsWith(GitHeader, StringComparison.Ordinal) ? HeaderPath(line[GitHeader.Length..])
                : line.StartsWith(CombinedHeader, StringComparison.Ordinal) ? Unquote(line[CombinedHeader.Length..])
                : null;
            if (path is null)
            {
                return false;
            }

            (_parents, _oldPath, _renameFrom, _oldMode) = (0, null, null, null);
            _fileRow = Rows.Count;
            Rows.Add(new GitDiffRow(GitDiffRowKind.File, path));
            return true;
        }

        // "@@ -12,7 +12,8 @@ void Method()" or combined "@@@ -1,2 -1,2 +1,3 @@@": there are as many ranges as '@' marks.
        private bool TryStartHunk(string line)
        {
            if (!line.StartsWith(HunkMark, StringComparison.Ordinal))
            {
                return false;
            }

            var marks = line.Length - line.AsSpan().TrimStart('@').Length;
            var end = line.IndexOf(new string('@', marks), marks, StringComparison.Ordinal);
            var ranges = end < 0 ? [] : line[marks..end].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ranges.Length != marks)
            {
                return false;
            }

            _parents = marks - 1;
            (_oldLine, _oldLeft) = _parents == 1 ? Range(ranges[0]) : (0, 0);
            (_newLine, _newLeft) = Range(ranges[^1]);
            Rows.Add(new GitDiffRow(GitDiffRowKind.Hunk, line));
            return true;
        }

        private void ReadHunkLine(string line)
        {
            var mark = line.Length == 0 ? ' ' : line[0];
            var text = line.Length == 0 ? string.Empty : line[1..];
            switch (mark)
            {
                case ' ':
                    Rows.Add(new GitDiffRow(GitDiffRowKind.Context, text, _oldLine++, _newLine++));
                    (_oldLeft, _newLeft) = (_oldLeft - 1, _newLeft - 1);
                    break;
                case '-':
                    Rows.Add(new GitDiffRow(GitDiffRowKind.Removed, text, _oldLine++));
                    _oldLeft--;
                    break;
                case '+':
                    Rows.Add(new GitDiffRow(GitDiffRowKind.Added, text, NewLine: _newLine++));
                    _newLeft--;
                    break;
                default:
                    ReadMeta(line);
                    return;
            }

            // The hunk is over; a following "\ No newline at end of file" is parsed as a note.
            if (_oldLeft <= 0 && _newLeft <= 0)
            {
                _parents = 0;
            }
        }

        // One marker column per parent: any '-' means the line was removed, any '+' means it was added.
        private void ReadCombinedLine(string line)
        {
            if (line.Length < _parents || line.AsSpan(0, _parents).ContainsAnyExcept(" +-"))
            {
                ReadMeta(line);
                return;
            }

            var marks = line.AsSpan(0, _parents);
            var text = line[_parents..];
            Rows.Add(marks.Contains('-')
                ? new GitDiffRow(GitDiffRowKind.Removed, text)
                : new GitDiffRow(marks.Contains('+') ? GitDiffRowKind.Added : GitDiffRowKind.Context, text, NewLine: _newLine++));
        }

        private void ReadMeta(string line)
        {
            if (MetaNote(line) is { } note)
            {
                Rows.Add(new GitDiffRow(GitDiffRowKind.Note, note));
            }
        }

        private string? MetaNote(string line)
        {
            if (line.StartsWith('\\'))
            {
                return Strings.DiffNoNewline;
            }

            if (TryValue(line, "--- ", out var oldPath))
            {
                _oldPath = oldPath;
            }
            else if (TryValue(line, "+++ ", out var newPath))
            {
                UpdateFilePath(newPath);
            }
            else if (TryValue(line, "old mode ", out var oldMode))
            {
                _oldMode = oldMode;
            }
            else if (TryValue(line, "rename from ", out var from) || TryValue(line, "copy from ", out from))
            {
                _renameFrom = Unquote(from);
            }
            else
            {
                return Note(line);
            }

            return null;
        }

        private string? Note(string line) =>
            line.StartsWith("new file mode", StringComparison.Ordinal) ? Strings.DiffNewFile
            : line.StartsWith("deleted file mode", StringComparison.Ordinal) ? Strings.DiffDeletedFile
            : TryValue(line, "new mode ", out var mode) ? Format(Strings.DiffModeChanged, _oldMode ?? "?", mode)
            : TryValue(line, "rename to ", out var to) ? Format(Strings.DiffRenamed, _renameFrom ?? "?", Unquote(to))
            : TryValue(line, "copy to ", out to) ? Format(Strings.DiffCopied, _renameFrom ?? "?", Unquote(to))
            : line.StartsWith("Binary files ", StringComparison.Ordinal) || line == "GIT binary patch" ? Strings.DiffBinary
            : null;

        // "+++ b/path" ("--- a/path" for a deleted file) is the exact path; "diff --git" is ambiguous with spaces.
        private void UpdateFilePath(string newPath)
        {
            var path = newPath == DevNull ? _oldPath : newPath;
            if (_fileRow < 0 || path is null || path == DevNull)
            {
                return;
            }

            Rows[_fileRow] = Rows[_fileRow] with { Text = WithoutPrefix(Unquote(path)) };
        }

        // "a/path b/path": equal halves give the path; otherwise (a rename) everything after the last " b/".
        private static string HeaderPath(string rest)
        {
            var half = (rest.Length - 1) / 2;
            if (half > PrefixLength && rest.Length % 2 == 1 && rest[half] == ' '
                && rest.AsSpan(PrefixLength, half - PrefixLength).SequenceEqual(rest.AsSpan(half + 1 + PrefixLength)))
            {
                return rest[PrefixLength..half];
            }

            var quoted = rest.LastIndexOf(" \"b/", StringComparison.Ordinal);
            var plain = rest.LastIndexOf(" b/", StringComparison.Ordinal);
            return quoted >= 0 ? WithoutPrefix(Unquote(rest[(quoted + 1)..])) : plain >= 0 ? rest[(plain + 3)..] : rest;
        }

        private static (int Start, int Count) Range(string range)
        {
            var parts = range[1..].Split(',');
            var start = Number(parts[0]);
            return (start, parts.Length > 1 ? Number(parts[1]) : 1);
        }

        private static int Number(string text) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private static bool TryValue(string line, string prefix, out string value)
        {
            var match = line.StartsWith(prefix, StringComparison.Ordinal);
            value = match ? line[prefix.Length..].TrimEnd('\t') : string.Empty;
            return match;
        }

        private static string WithoutPrefix(string path) =>
            path.Length > PrefixLength && path[1] == '/' && path[0] is 'a' or 'b' ? path[PrefixLength..] : path;

        // git quotes and escapes paths with control characters or quotes: "a/x\"y".
        private static string Unquote(string path)
        {
            if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
            {
                return path;
            }

            var text = new StringBuilder(path.Length);
            for (var i = 1; i < path.Length - 1; i++)
            {
                var character = path[i];
                if (character == '\\' && i + 1 < path.Length - 1)
                {
                    character = path[++i] switch { 't' => '\t', 'n' => '\n', var other => other };
                }

                text.Append(character);
            }

            return text.ToString();
        }

        private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
    }
}
