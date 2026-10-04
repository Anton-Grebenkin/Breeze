using CodeEditor.Core.Text;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// File order in a group: natural, as in Explorer (<c>file2</c> before <c>file10</c>). Paths differing only in case are
/// ordered by code point, so the order is total and the panel row merge does not confuse them.
/// </summary>
public sealed class GitPathComparer : IComparer<string>
{
    public static GitPathComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var natural = NaturalStringComparer.Instance.Compare(x, y);
        return natural != 0 ? natural : string.CompareOrdinal(x, y);
    }
}
