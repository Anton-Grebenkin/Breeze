using CodeEditor.Shell.Palette;

namespace CodeEditor.Testing;

/// <summary>Quick pick without the palette: the test inspects the items and "picks" one.</summary>
public sealed class FakeQuickPick : IQuickPick
{
    public IQuickOpenProvider? Shown { get; private set; }

    /// <summary>Items of the shown list for an empty query.</summary>
    public IReadOnlyList<PaletteItem> Items => Shown?.Filter(string.Empty) ?? [];

    public void Show(IQuickOpenProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.Prepare();
        Shown = provider;
    }

    /// <summary>Types the text and accepts the first match, like Enter in the palette.</summary>
    public Task PickAsync(string text)
    {
        var provider = Shown ?? throw new InvalidOperationException("Список не показан.");
        return provider.AcceptAsync(provider.Filter(text)[0], text);
    }
}
