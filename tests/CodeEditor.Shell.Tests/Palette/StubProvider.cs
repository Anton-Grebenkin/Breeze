using CodeEditor.Shell.Palette;

namespace CodeEditor.Shell.Tests.Palette;

/// <summary>Default mode for palette tests: one row for any query.</summary>
internal sealed class StubProvider : IQuickOpenProvider
{
    public const string Title = "stub";

    public string Prefix => string.Empty;

    public string Placeholder => "Файлы";

    public string EmptyText => "Пусто";

    public event EventHandler? ItemsChanged;

    public int PrepareCount { get; private set; }

    public bool HasSubscribers => ItemsChanged is not null;

    public string? AcceptedText { get; private set; }

    public void Prepare() => PrepareCount++;

    public void RaiseItemsChanged() => ItemsChanged?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<PaletteItem> Filter(string text) => [new PaletteItem("stub", Title, null, [], false)];

    public Task AcceptAsync(PaletteItem item, string text)
    {
        AcceptedText = text;
        return Task.CompletedTask;
    }
}
