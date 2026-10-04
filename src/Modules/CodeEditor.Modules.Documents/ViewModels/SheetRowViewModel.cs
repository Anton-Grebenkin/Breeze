namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>
/// A grid row in the viewer: its number and the values of non-empty cells only, so empty cells take no memory. A view
/// column binds to the indexer: <c>[3]</c> is column C.
/// </summary>
public sealed class SheetRowViewModel(int number, IReadOnlyDictionary<int, string> cells)
{
    public int Number { get; } = number;

    /// <param name="column">1-based column.</param>
    public string this[int column] => cells.TryGetValue(column, out var value) ? value : string.Empty;
}
