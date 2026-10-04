using CodeEditor.Core.Commands;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Shell.Tests.Palette;

public sealed class PaletteFilterTests
{
    private static readonly PaletteCandidate[] Candidates =
    [
        Candidate("theme.toggle", "Переключить тему", "Оформление", "Ctrl+K Ctrl+T"),
        Candidate("theme.dark", "Тёмная тема", "Оформление"),
        Candidate("theme.light", "Светлая тема", "Оформление"),
        Candidate("app.quit", "Выход", "Файл", "Alt+F4"),
    ];

    [Fact]
    public void EmptyQuery_ListsAllAlphabetically()
    {
        var items = PaletteFilter.Apply(Candidates, "  ", []);

        Assert.Equal(
            ["Оформление: Переключить тему", "Оформление: Светлая тема", "Оформление: Тёмная тема", "Файл: Выход"],
            items.Select(item => item.Title));
        Assert.All(items, item => Assert.Empty(item.Highlights));
    }

    [Fact]
    public void EmptyQuery_PutsRecentFirstInRecencyOrder()
    {
        var items = PaletteFilter.Apply(Candidates, string.Empty, ["app.quit", "theme.dark"]);

        Assert.Equal(["app.quit", "theme.dark", "theme.toggle", "theme.light"], items.Select(item => item.Id));
        Assert.Equal([true, true, false, false], items.Select(item => item.IsRecent));
    }

    [Fact]
    public void Query_KeepsOnlyMatchesWithHighlights()
    {
        var items = PaletteFilter.Apply(Candidates, "свет", []);

        var item = Assert.Single(items);
        Assert.Equal("theme.light", item.Id);
        Assert.Equal([12, 13, 14, 15], item.Highlights);
    }

    [Fact]
    public void Query_RanksWordStartsFirst()
    {
        // Both query letters start words in the theme.dark title but sit mid-word in the others.
        var items = PaletteFilter.Apply(Candidates, "тт", []);

        Assert.Equal("theme.dark", items[0].Id);
    }

    [Fact]
    public void Query_WithEqualScores_PrefersRecentOverAlphabet()
    {
        // theme.light and theme.dark match with equal scores; without recent commands the order is alphabetical.
        var withoutRecent = PaletteFilter.Apply(Candidates, "оформление", []);
        var withRecent = PaletteFilter.Apply(Candidates, "оформление", ["theme.dark"]);

        Assert.Equal("theme.light", withoutRecent[0].Id);
        Assert.Equal("theme.dark", withRecent[0].Id);
    }

    [Fact]
    public void Query_KeepsShortcut()
    {
        var item = Assert.Single(PaletteFilter.Apply(Candidates, "выход", []));

        Assert.Equal("Alt+F4", item.Shortcut);
    }

    private static PaletteCandidate Candidate(string id, string title, string category, string? shortcut = null) =>
        new(new CommandDefinition(id, title, (_, _) => ValueTask.CompletedTask, category), shortcut);
}
