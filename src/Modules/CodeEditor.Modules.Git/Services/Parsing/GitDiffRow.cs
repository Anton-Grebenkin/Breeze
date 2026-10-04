using System.Globalization;

namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A diff display row: kind, text without the "+"/"-" marker, old and new line numbers.</summary>
/// <param name="OldLine">1-based line number in the old version; <c>0</c> when absent there.</param>
/// <param name="NewLine">1-based line number in the new version; <c>0</c> when absent there.</param>
public sealed record GitDiffRow(GitDiffRowKind Kind, string Text, int OldLine = 0, int NewLine = 0)
{
    public string OldNumber => Number(OldLine);

    public string NewNumber => Number(NewLine);

    /// <summary>Marker shown left of the text.</summary>
    public string Marker => Kind switch
    {
        GitDiffRowKind.Added => "+",
        GitDiffRowKind.Removed => "-",
        _ => string.Empty,
    };

    private static string Number(int line) => line > 0 ? line.ToString(CultureInfo.InvariantCulture) : string.Empty;
}
