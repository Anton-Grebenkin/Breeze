using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Parses an <c>apply_patch</c> patch (V4A, as in Codex; known to GPT-5 and newer, ADR 0010): <c>*** Update File</c>
/// blocks ("@@ anchor", " context", "-removed", "+added" lines) become before → after edits (<see cref="FileEdit"/>)
/// that follow the <c>apply_edits</c> path; <c>*** Add File</c> creates a file. Deletes and moves belong to
/// <c>delete_file</c> and <c>move_file</c> and produce a clear error here. O(n) in patch lines.
/// </summary>
public static class PatchParser
{
    private const string Begin = "*** Begin Patch";
    private const string End = "*** End Patch";
    private const string EndOfFile = "*** End of File";
    private const string Update = "*** Update File:";
    private const string Add = "*** Add File:";
    private const string Delete = "*** Delete File:";
    private const string Move = "*** Move to:";
    private const string HeaderPrefix = "*** ";
    private const string AnchorPrefix = "@@";

    /// <exception cref="AgentToolException">Not a V4A patch, or it asks for what other tools do.</exception>
    public static ParsedPatch Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var lines = input.ReplaceLineEndings("\n").Split('\n');
        var index = Array.FindIndex(lines, line => line.Trim() == Begin);
        index = index < 0 ? Array.FindIndex(lines, line => line.StartsWith(HeaderPrefix, StringComparison.Ordinal)) : index + 1;
        if (index < 0)
        {
            throw Error(Strings.PatchStart, Begin, Update, Add);
        }

        var edits = new List<FileEdit>();
        var additions = new List<PatchAddition>();
        while (index < lines.Length && lines[index].Trim() != End)
        {
            var line = lines[index++].TrimEnd();
            if (line.StartsWith(Update, StringComparison.Ordinal))
            {
                index = ParseUpdate(lines, index, Path(line, Update), edits);
            }
            else if (line.StartsWith(Add, StringComparison.Ordinal))
            {
                index = ParseAddition(lines, index, Path(line, Add), additions);
            }
            else if (line.StartsWith(Delete, StringComparison.Ordinal))
            {
                throw new AgentToolException(Strings.PatchNoDelete);
            }
            else if (line.Length > 0)
            {
                throw Error(Strings.PatchUnknownLine, line, Update, Add);
            }
        }

        return edits.Count + additions.Count > 0 ? new ParsedPatch(edits, additions) : throw new AgentToolException(Strings.PatchEmpty);
    }

    private static string Path(string header, string prefix) =>
        header[prefix.Length..].Trim() is { Length: > 0 } path ? path : throw Error(Strings.PatchNoPath, prefix);

    private static int ParseUpdate(string[] lines, int index, string path, List<FileEdit> edits)
    {
        if (index < lines.Length && lines[index].StartsWith(Move, StringComparison.Ordinal))
        {
            throw Error(Strings.PatchNoMove, path);
        }

        var hunk = new Hunk(path);
        for (; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimEnd() == EndOfFile)
            {
                continue;
            }

            if (line.StartsWith(HeaderPrefix, StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith(AnchorPrefix, StringComparison.Ordinal))
            {
                hunk.Flush(edits);
                hunk.Anchor(line[AnchorPrefix.Length..]);
                continue;
            }

            hunk.Add(line);
        }

        hunk.Flush(edits);
        return index;
    }

    private static int ParseAddition(string[] lines, int index, string path, List<PatchAddition> additions)
    {
        var content = new List<string>();
        for (; index < lines.Length && !lines[index].StartsWith(HeaderPrefix, StringComparison.Ordinal); index++)
        {
            if (lines[index].StartsWith('+'))
            {
                content.Add(lines[index][1..]);
            }
        }

        additions.Add(new PatchAddition(path, string.Join('\n', content) + "\n"));
        return index;
    }

    private static AgentToolException Error(string format, params object[] arguments) =>
        new(string.Format(CultureInfo.CurrentCulture, format, arguments));

    /// <summary>
    /// A hunk: context and removed lines form "before", context and added lines "after"; anchors give the place.
    /// </summary>
    private sealed class Hunk(string path)
    {
        private readonly List<string> _anchors = [];
        private readonly List<string> _old = [];
        private readonly List<string> _new = [];
        private bool _changed;

        public void Anchor(string text)
        {
            if (text.Trim().Length > 0)
            {
                _anchors.Add(text.Trim());
            }
        }

        // A line without a prefix lost its context space (common for blank lines): treat it as context.
        public void Add(string line)
        {
            var (kind, text) = line.Length == 0 ? (' ', string.Empty) : line[0] is ' ' or '-' or '+' ? (line[0], line[1..]) : (' ', line);
            if (kind != '+')
            {
                _old.Add(text);
            }

            if (kind != '-')
            {
                _new.Add(text);
            }

            _changed |= kind != ' ';
        }

        // Consecutive anchors ("@@ class B", "@@ void Go()") accumulate for the next hunk; nothing to flush without lines.
        public void Flush(List<FileEdit> edits)
        {
            if (_old.Count == 0 && _new.Count == 0)
            {
                return;
            }

            if (_changed)
            {
                edits.Add(Edit());
            }

            _old.Clear();
            _new.Clear();
            _anchors.Clear();
            _changed = false;
        }

        // A pure insertion without context goes right after the last anchor line.
        private FileEdit Edit()
        {
            if (_old.All(line => line.Length == 0) && _old.Count <= 1)
            {
                if (_anchors.Count == 0)
                {
                    throw Error(Strings.PatchNoContext, path);
                }

                var anchor = _anchors[^1];
                return new FileEdit(path, anchor, anchor + "\n" + string.Join('\n', _new)) { Anchors = [.. _anchors.SkipLast(1)] };
            }

            return new FileEdit(path, string.Join('\n', _old), string.Join('\n', _new)) { Anchors = [.. _anchors] };
        }
    }
}
