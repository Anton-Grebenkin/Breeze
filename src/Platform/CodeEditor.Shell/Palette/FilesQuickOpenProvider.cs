using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Resources;

namespace CodeEditor.Shell.Palette;

/// <summary>
/// The default mode, quick open for files (<c>Ctrl+P</c>): fuzzy search by name, then by path, recent files first,
/// "name:line" opens the file at a line. An empty query lists recent files.
/// </summary>
public sealed class FilesQuickOpenProvider(
    IFileIndex index,
    IWorkspace workspace,
    RecentFiles recentFiles,
    ICommandService commands,
    IUiDispatcher dispatcher) : IQuickOpenProvider
{
    private IReadOnlyList<IndexedFile> _files = [];
    private Dictionary<string, int> _recentRank = [];
    private EventHandler? _itemsChanged;

    // Subscribes to the index only while the mode is open: the palette subscribes on entry and unsubscribes on exit.
    public event EventHandler? ItemsChanged
    {
        add
        {
            if (_itemsChanged is null)
            {
                index.Changed += OnIndexChanged;
            }

            _itemsChanged += value;
        }

        remove
        {
            _itemsChanged -= value;
            if (_itemsChanged is null)
            {
                index.Changed -= OnIndexChanged;
            }
        }
    }

    public string Prefix => string.Empty;

    public string Placeholder => Strings.FilesPlaceholder;

    public string EmptyText => workspace.Root is null
        ? Strings.NoFolderOpen
        : Strings.NoMatchingFiles;

    public void Prepare()
    {
        _files = index.Files;
        _recentRank = recentFiles.Items
            .Select((path, rank) => (path, rank))
            .ToDictionary(pair => pair.path, pair => pair.rank, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<PaletteItem> Filter(string text)
    {
        var (pattern, _) = SplitLine(text.Trim());
        return pattern.Length == 0 ? FileMatcher.Recent(_files, _recentRank) : FileMatcher.Match(_files, pattern, _recentRank);
    }

    public async Task AcceptAsync(PaletteItem item, string text)
    {
        await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(item.Id));

        // As in VS Code: after Ctrl+P the user types in the opened file.
        if (SplitLine(text.Trim()).Line is { } line)
        {
            await commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, line);
        }
        else
        {
            await commands.ExecuteAsync(ShellCommandIds.FocusActiveEditor);
        }
    }

    /// <summary>"Program.cs:42" → ("Program.cs", 42).</summary>
    public static (string Pattern, int? Line) SplitLine(string text)
    {
        var colon = text.LastIndexOf(':');
        return colon >= 0 && int.TryParse(text.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line > 0
            ? (text[..colon], line)
            : (text, null);
    }

    // The index raises Changed on a background thread.
    private void OnIndexChanged(object? sender, EventArgs e) => dispatcher.Post(() => _itemsChanged?.Invoke(this, EventArgs.Empty));
}
