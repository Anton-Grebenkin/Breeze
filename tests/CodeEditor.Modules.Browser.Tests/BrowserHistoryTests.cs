using CodeEditor.Core.Storage;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Browser.Tests;

/// <summary>Browser history: pages the user or the agent opened, newest first, kept across restarts.</summary>
public sealed class BrowserHistoryTests : IDisposable
{
    private static readonly string UserData = Path.GetFullPath(@"C:\user");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(UserData);
    private readonly FakeBrowserEngine _browser = new();
    private readonly ManualTimeProvider _time = new();
    private readonly BrowserHistory _history;

    public BrowserHistoryTests() => _history = Create();

    public void Dispose() => _history.Dispose();

    [Fact]
    public async Task LoadedPages_NewestFirst_TitleArrivesLater_KeptAcrossRestart()
    {
        _browser.Load("http://localhost:5000/", string.Empty);
        _browser.Load("http://localhost:5000/", "Главная");
        _browser.Load("http://localhost:5000/orders", "Заказы");
        _browser.Load("http://localhost:5000/", string.Empty);

        Assert.Equal([("http://localhost:5000/", "Главная"), ("http://localhost:5000/orders", "Заказы")], _history.Visits.Select(visit => (visit.Url, visit.Title)));
        await _history.Saved;
        using var reopened = Create();
        Assert.Equal(_history.Visits, reopened.Visits);
    }

    [Fact]
    public void PagesOtherThanWeb_AreNotRecorded()
    {
        _browser.Load("about:blank", string.Empty);

        Assert.Empty(_history.Visits);
    }

    [Fact]
    public async Task Capacity_KeepsTheNewest_ClearEmptiesIt()
    {
        for (var index = 0; index < BrowserHistory.Capacity + 5; index++)
        {
            _browser.Load($"http://localhost:5000/p{index}", $"P{index}");
        }

        Assert.Equal(BrowserHistory.Capacity, _history.Visits.Count);
        Assert.Equal($"http://localhost:5000/p{BrowserHistory.Capacity + 4}", _history.Visits[0].Url);
        _history.Clear();
        await _history.Saved;
        using var reopened = Create();
        Assert.Empty(reopened.Visits);
    }

    [Fact]
    public void HistoryMenu_RecentPages_ThenAllAndClear()
    {
        _browser.Load("http://localhost:5000/orders?page=2", "Заказы");
        using var panel = new BrowserViewModel(_browser, _history);

        panel.ShowHistoryCommand.Execute(null);

        Assert.True(panel.IsHistoryOpen);
        Assert.Equal(["Заказы — localhost:5000/orders?page=2", "—", "Все страницы…", "Очистить историю"], panel.HistoryMenu.Select(item => item.ToString()));
    }

    [Fact]
    public void HistoryMenu_Empty_SaysSo()
    {
        using var panel = new BrowserViewModel(_browser, _history);

        panel.ShowHistoryCommand.Execute(null);

        Assert.Equal("Страниц пока нет", Assert.Single(panel.HistoryMenu).Header);
    }

    private BrowserHistory Create() => new(_browser, _fileSystem, new UserDataPaths(UserData), _time, NullLogger<BrowserHistory>.Instance);
}
