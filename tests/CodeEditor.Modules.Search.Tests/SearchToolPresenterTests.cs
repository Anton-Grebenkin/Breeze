using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Search.Services.Agent;

namespace CodeEditor.Modules.Search.Tests;

public sealed class SearchToolPresenterTests
{
    private readonly SearchToolPresenter _presenter = new();

    [Theory]
    [InlineData("src/a.cs\n3: Reserve(lines);\n4- // контекст\nsrc/b.cs\n10: Reserve();", "2 совпадения в 2 файлах")]
    [InlineData("src/a.cs (5)\nsrc/b.cs (1)", "6 совпадений в 2 файлах")]
    [InlineData("Совпадений: 7 в 3 файлах.\nsrc/a.cs: 5", "совпадений: 7 в 3 файлах")]
    [InlineData("(ничего не найдено)", "ничего не найдено")]
    public void Detail_CountsMatchesAndFiles(string result, string detail)
    {
        var view = _presenter.Present(new AgentToolCall(SearchAgentTools.SearchTextName, new Dictionary<string, object?> { ["query"] = "Reserve" }, result))!;

        Assert.Equal(("Поиск «Reserve»", AgentToolIcon.Search, true), (view.Title, view.Icon, view.IsExploration));
        Assert.Equal(detail, view.Detail);
    }

    [Fact]
    public void Running_HasNoDetail() =>
        Assert.Null(_presenter.Present(new AgentToolCall(SearchAgentTools.SearchTextName, new Dictionary<string, object?> { ["query"] = "x" }))!.Detail);
}
