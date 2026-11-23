using CodeEditor.Core.Storage;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>
/// Address suggestions: typing into the bar offers pages from the history and known sites; the arrows choose, Enter
/// opens the chosen one, and an address the page itself reports offers nothing.
/// </summary>
public sealed class BrowserAddressSuggestionsTests : IDisposable
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData);
    private readonly BrowserTabs _tabs;
    private readonly FakeBrowserPage _page;
    private readonly BrowserHistory _history;
    private readonly BrowserKnownSites _sites;

    public BrowserAddressSuggestionsTests()
    {
        _tabs = FakeBrowserEngine.Tabs(out _page);
        _history = new BrowserHistory(_tabs, _fileSystem, new UserDataPaths(UserData), new ManualTimeProvider(), NullLogger<BrowserHistory>.Instance);
        _sites = new BrowserKnownSites(_tabs, _fileSystem, new UserDataPaths(UserData), NullLogger<BrowserKnownSites>.Instance);
    }

    public void Dispose()
    {
        _history.Dispose();
        _sites.Dispose();
    }

    [Fact]
    public void TypedText_OffersMatchingPages_AddressStartFirst_ThenInside_ThenTitle()
    {
        _page.Load("https://docs.example.net/handbook", "Git handbook");
        _page.Load("https://www.example.org/git-guide", "Guide");
        _page.Load("https://grafana.example.com/d/abc", "Orders");
        _page.Load("https://gitlab.example.com/shop/-/pipelines", "Pipelines");
        var suggestions = new BrowserAddressSuggestions(_history);

        suggestions.Update(" Git ");

        Assert.True(suggestions.IsOpen);
        Assert.Equal(-1, suggestions.SelectedIndex);
        Assert.Equal(
            [
                new BrowserSuggestion("https://gitlab.example.com/shop/-/pipelines", "Pipelines", "gitlab.example.com/shop/-/pipelines"),
                new BrowserSuggestion("https://www.example.org/git-guide", "Guide", "example.org/git-guide"),
                new BrowserSuggestion("https://docs.example.net/handbook", "Git handbook", "docs.example.net/handbook"),
            ],
            suggestions.Items);
    }

    [Fact]
    public void TypedScheme_IsIgnored_NoMatch_OrNoText_OffersNothing()
    {
        _page.Load("https://gitlab.example.com/shop", "Shop");
        var suggestions = new BrowserAddressSuggestions(_history);

        suggestions.Update("https://www.gitlab");
        Assert.Equal("gitlab.example.com/shop", Assert.Single(suggestions.Items).Address);

        suggestions.Update("jenkins");
        Assert.False(suggestions.IsOpen);

        suggestions.Update("  ");
        Assert.False(suggestions.IsOpen);
    }

    [Fact]
    public void KnownSite_IsOfferedByName_WhenTheHistoryIsCleared()
    {
        _page.Load("https://grafana.example.com/d/abc", "Orders");
        _history.Clear();
        var suggestions = new BrowserAddressSuggestions(_history, _sites);

        suggestions.Update("graf");

        Assert.Equal([new BrowserSuggestion("https://grafana.example.com/", string.Empty, "grafana.example.com")], suggestions.Items);
    }

    [Fact]
    public void List_IsCapped()
    {
        for (var index = 0; index < BrowserAddressSuggestions.MaxItems + 4; index++)
        {
            _page.Load($"https://gitlab.example.com/p{index}", $"P{index}");
        }

        var suggestions = new BrowserAddressSuggestions(_history);
        suggestions.Update("gitlab");

        Assert.Equal(BrowserAddressSuggestions.MaxItems, suggestions.Items.Count);
        Assert.Equal($"https://gitlab.example.com/p{BrowserAddressSuggestions.MaxItems + 3}", suggestions.Items[0].Url);
    }

    [Fact]
    public void Arrows_WalkTheList_ThroughTheTypedText_TakeGivesTheChosenAndCloses()
    {
        _page.Load("https://gitlab.example.com/a", "A");
        _page.Load("https://gitlab.example.com/b", "B");
        var suggestions = new BrowserAddressSuggestions(_history);
        suggestions.Update("gitlab");

        suggestions.NextCommand.Execute(null);
        suggestions.NextCommand.Execute(null);
        Assert.Equal(1, suggestions.SelectedIndex);
        suggestions.NextCommand.Execute(null);
        Assert.Equal(-1, suggestions.SelectedIndex);
        suggestions.PreviousCommand.Execute(null);
        Assert.Equal(1, suggestions.SelectedIndex);

        Assert.Equal("https://gitlab.example.com/a", suggestions.Take()?.Url);
        Assert.False(suggestions.IsOpen);
        Assert.Null(suggestions.Take());
    }

    [Fact]
    public void Escape_ClosesTheList_ArrowsThenDoNothing()
    {
        _page.Load("https://gitlab.example.com/a", "A");
        var suggestions = new BrowserAddressSuggestions(_history);
        suggestions.Update("gitlab");

        suggestions.DismissCommand.Execute(null);
        suggestions.NextCommand.Execute(null);

        Assert.False(suggestions.IsOpen);
        Assert.Equal(-1, suggestions.SelectedIndex);
    }

    [Fact]
    public async Task Panel_TypingOffersPages_EnterOpensTheChosenOne_ThePageAddressOffersNothing()
    {
        _page.Load("https://gitlab.example.com/shop/-/pipelines", "Shop pipelines");
        _page.Load("https://grafana.example.com/d/abc", "Orders");
        using var panel = new BrowserViewModel(_tabs, new InlineUiDispatcher(), _history, sites: _sites);
        Assert.False(panel.Suggestions.IsOpen);

        panel.Address = "shop pipe";

        Assert.True(panel.Suggestions.IsOpen);
        Assert.False(panel.GoCommand.CanExecute(null));
        panel.Suggestions.NextCommand.Execute(null);
        Assert.True(panel.GoCommand.CanExecute(null));
        await panel.GoCommand.ExecuteAsync(null);

        Assert.Equal(new Uri("https://gitlab.example.com/shop/-/pipelines"), _page.Navigations[^1]);
        Assert.Equal("https://gitlab.example.com/shop/-/pipelines", panel.Address);
        Assert.False(panel.Suggestions.IsOpen);
    }

    [Fact]
    public async Task Panel_MouseChoice_OpensTheSuggestion_EnterWithoutChoiceOpensTheTypedAddress()
    {
        _page.Load("https://gitlab.example.com/shop", "Shop");
        using var panel = new BrowserViewModel(_tabs, new InlineUiDispatcher(), _history);

        panel.Address = "gitlab";
        panel.Suggestions.Choose(panel.Suggestions.Items[0]);
        await panel.GoCommand.ExecuteAsync(null);
        Assert.Equal(new Uri("https://gitlab.example.com/shop"), _page.Navigations[^1]);

        panel.Address = "gitlab.example.com/other";
        await panel.GoCommand.ExecuteAsync(null);
        Assert.Equal(new Uri("https://gitlab.example.com/other"), _page.Navigations[^1]);
    }
}
