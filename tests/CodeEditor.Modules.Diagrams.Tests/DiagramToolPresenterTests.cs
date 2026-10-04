using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Diagrams.Services.Agent;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>Agent feed lines for the <c>diagram</c> tool: what was done with which file; view counts as exploration.</summary>
public sealed class DiagramToolPresenterTests
{
    private readonly DiagramToolPresenter _presenter = new();

    [Theory]
    [InlineData("check", "docs/arch.mmd", null, null, "Проверка схемы docs/arch.mmd", false)]
    [InlineData("view", "docs/README.md", 2, null, "Просмотр схемы docs/README.md #2", true)]
    [InlineData("render", "docs/arch.mmd", null, "png", "Схема docs/arch.mmd → PNG", false)]
    [InlineData("render", "docs/arch.mmd", null, null, "Схема docs/arch.mmd → SVG", false)]
    [InlineData("check", null, null, null, "Проверка схемы (текст)", false)]
    public void Rows_DescribeTheCall(string action, string? path, int? block, string? format, string title, bool exploration)
    {
        var view = _presenter.Present(new AgentToolCall("diagram", new Dictionary<string, object?>
        {
            ["action"] = action,
            ["path"] = path,
            ["block"] = block,
            ["format"] = format,
        }));

        Assert.NotNull(view);
        Assert.Equal((AgentToolIcon.Image, title, path, exploration), (view.Icon, view.Title, view.FilePath, view.IsExploration));
    }

    [Fact]
    public void OtherTools_AreNotDescribed() =>
        Assert.Null(_presenter.Present(new AgentToolCall("git", new Dictionary<string, object?>())));
}
