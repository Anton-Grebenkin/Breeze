using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Approvals;

/// <summary>One file's change in an approval card: header, counters and diff hunks.</summary>
public sealed class FileDiffViewModel
{
    /// <summary>Caps the shown diff so a long new file does not bloat the chat.</summary>
    public const int MaxLines = 400;

    public FileDiffViewModel(FileChangePreview change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Header = change.Header ?? change.Kind switch
        {
            ProposedChangeKind.Create => Format(Strings.DiffNewFile, change.RelativePath),
            ProposedChangeKind.Delete => Format(Strings.DiffDelete, change.RelativePath),
            ProposedChangeKind.Move => Format(Strings.DiffMove, change.RelativePath, change.NewRelativePath),
            ProposedChangeKind.Command => change.RelativePath is "." or ""
                ? Strings.DiffCommandInProject
                : Format(Strings.DiffCommandInFolder, change.RelativePath),
            _ => Format(Strings.DiffEdit, change.RelativePath),
        };

        // A command is not a file edit: show the command itself, without a diff or counters.
        IsCommand = change.Kind == ProposedChangeKind.Command;
        Command = IsCommand ? change.NewText : string.Empty;
        var lines = change.Kind is ProposedChangeKind.Move or ProposedChangeKind.Command ? [] : LineDiff.Compute(change.OldText, change.NewText);
        Added = lines.Count(line => line.Kind == DiffKind.Added);
        Removed = lines.Count(line => line.Kind == DiffKind.Removed);
        Lines = BuildLines(lines);
    }

    public string Header { get; }

    public bool IsCommand { get; }

    /// <summary>Command text for the card; empty for file edits.</summary>
    public string Command { get; }

    public int Added { get; }

    public int Removed { get; }

    public string Counts => Added + Removed == 0 ? string.Empty : $"+{Added} −{Removed}";

    public IReadOnlyList<DiffLineViewModel> Lines { get; }

    private static List<DiffLineViewModel> BuildLines(IReadOnlyList<DiffLine> lines)
    {
        var result = new List<DiffLineViewModel>();
        foreach (var hunk in LineDiff.Hunks(lines))
        {
            if (result.Count > 0)
            {
                result.Add(new DiffLineViewModel(DiffKind.Unchanged, string.Empty, 0, IsSeparator: true));
            }

            result.AddRange(hunk.Lines.Select(line => new DiffLineViewModel(line.Kind, line.Text, line.Kind == DiffKind.Removed ? line.OldLine : line.NewLine)));
            if (result.Count >= MaxLines)
            {
                result.Add(new DiffLineViewModel(DiffKind.Unchanged, Format(Strings.DiffMoreLines, lines.Count - MaxLines), 0, IsSeparator: true));
                break;
            }
        }

        return result;
    }

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
