using CodeEditor.Core.Context;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.Palette;

/// <summary>
/// The palette: a single quick input field, as in VS Code. The query prefix picks the mode: none for files
/// (<c>Ctrl+P</c>), ">" for commands (<c>Ctrl+Shift+P</c>), ":" for go to line (<c>Ctrl+G</c>). Works with the
/// keyboard (arrows, <c>Enter</c>, <c>Esc</c>) and the mouse (click a row, click outside).
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject, IQuickPick
{
    /// <summary>"Palette is open" context key for <c>when</c> clauses.</summary>
    public const string VisibleContextKey = "commandPaletteVisible";

    public const int PageSize = 10;

    private readonly IReadOnlyList<IQuickOpenProvider> _providers;
    private readonly IContextKeyService _context;
    private IQuickOpenProvider? _mode;

    // Quick pick mode (IQuickPick): stays until closed; query prefixes don't switch it.
    private IQuickOpenProvider? _pinned;

    /// <summary>Raised when a command opens the palette, including reopening in another mode; the view sets focus.</summary>
    public event EventHandler? Opened;

    public CommandPaletteViewModel(IEnumerable<IQuickOpenProvider> providers, IContextKeyService context)
    {
        // Longest prefix first, so the default mode (empty prefix) is checked last.
        _providers = [.. providers.OrderByDescending(provider => provider.Prefix.Length)];
        _context = context;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<PaletteItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string Placeholder { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string EmptyText { get; private set; } = string.Empty;

    /// <summary>The palette is open but nothing matches the query.</summary>
    public bool IsEmpty => IsOpen && Items.Count == 0;

    /// <summary>Opens the palette with initial text: ">" for commands, ":" for line, empty for files.</summary>
    public void Open(string initialText = CommandsQuickOpenProvider.CommandsPrefix) => OpenWith(null, initialText);

    /// <summary>Quick pick from a provider's list: the query starts empty and shows the provider's placeholder.</summary>
    public void Show(IQuickOpenProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        OpenWith(provider, string.Empty);
    }

    private void OpenWith(IQuickOpenProvider? pinned, string initialText)
    {
        SetMode(null);
        _pinned = pinned;
        IsOpen = true;
        _context.Set(VisibleContextKey, true);

        if (Query == initialText)
        {
            Refresh();
        }
        else
        {
            Query = initialText;
        }

        Opened?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _context.Set(VisibleContextKey, false);
        SetMode(null);
        _pinned = null;
        Items = [];
    }

    [RelayCommand]
    private void MoveNext() => Wrap(1);

    [RelayCommand]
    private void MovePrevious() => Wrap(-1);

    [RelayCommand]
    private void MovePageDown() => Clamp(PageSize);

    [RelayCommand]
    private void MovePageUp() => Clamp(-PageSize);

    [RelayCommand]
    private async Task AcceptAsync()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
        {
            await ExecuteItemAsync(Items[SelectedIndex]);
        }
    }

    [RelayCommand]
    private async Task ExecuteItemAsync(PaletteItem? item)
    {
        if (item is null || _mode is not { } mode)
        {
            return;
        }

        var text = TextFor(mode);
        Close();
        await mode.AcceptAsync(item, text);
    }

    partial void OnQueryChanged(string value)
    {
        if (IsOpen)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var mode = _pinned ?? _providers.FirstOrDefault(provider => Query.StartsWith(provider.Prefix, StringComparison.Ordinal));
        if (mode is null)
        {
            SetMode(null);
            Items = [];
            SelectedIndex = -1;
            return;
        }

        if (!ReferenceEquals(mode, _mode))
        {
            SetMode(mode);
            mode.Prepare();
            Placeholder = mode.Placeholder;
        }

        Filter(mode);
    }

    private void Filter(IQuickOpenProvider mode)
    {
        Items = mode.Filter(TextFor(mode));
        EmptyText = mode.EmptyText;
        SelectedIndex = Items.Count > 0 ? 0 : -1;
    }

    private void SetMode(IQuickOpenProvider? mode)
    {
        if (_mode is not null)
        {
            _mode.ItemsChanged -= OnItemsChanged;
        }

        _mode = mode;
        if (_mode is not null)
        {
            _mode.ItemsChanged += OnItemsChanged;
        }
    }

    // Candidates changed while open (e.g. the index finished building): rebuild, keeping the query.
    private void OnItemsChanged(object? sender, EventArgs e)
    {
        if (IsOpen && _mode is { } mode && ReferenceEquals(sender, mode))
        {
            mode.Prepare();
            Filter(mode);
        }
    }

    // In quick pick mode the whole query is the filter; other modes strip their prefix.
    private string TextFor(IQuickOpenProvider mode) => ReferenceEquals(mode, _pinned) ? Query : Query[mode.Prefix.Length..];

    private void Wrap(int delta)
    {
        if (Items.Count > 0)
        {
            SelectedIndex = ((SelectedIndex + delta) % Items.Count + Items.Count) % Items.Count;
        }
    }

    private void Clamp(int delta)
    {
        if (Items.Count > 0)
        {
            SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, Items.Count - 1);
        }
    }
}
