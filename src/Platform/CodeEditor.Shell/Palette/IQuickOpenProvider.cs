namespace CodeEditor.Shell.Palette;

/// <summary>
/// A quick input mode selected by prefix, as in VS Code: none for files, ">" for commands, ":" for line. Modules may
/// add their own modes.
/// </summary>
public interface IQuickOpenProvider
{
    /// <summary>The mode prefix; empty for the default mode.</summary>
    string Prefix { get; }

    string Placeholder { get; }

    /// <summary>Hint shown when nothing matches the query.</summary>
    string EmptyText { get; }

    /// <summary>
    /// Raised on the UI thread when candidates change while the mode is open (e.g. the file index finished building);
    /// the palette calls <see cref="Prepare"/> and filters again. Never raised by default.
    /// </summary>
    event EventHandler? ItemsChanged
    {
        add { }
        remove { }
    }

    /// <summary>Called on entering the mode: collects candidates (available commands, file index snapshot).</summary>
    void Prepare();

    /// <summary>Rows for the query text without the prefix.</summary>
    IReadOnlyList<PaletteItem> Filter(string text);

    /// <summary>Called when a row is chosen; the palette is already closed.</summary>
    Task AcceptAsync(PaletteItem item, string text);
}
