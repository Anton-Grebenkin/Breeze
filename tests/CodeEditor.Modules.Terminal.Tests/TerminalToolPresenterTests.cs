using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Tests;

public sealed class TerminalToolPresenterTests
{
    private readonly TerminalToolPresenter _presenter = new();

    [Theory]
    [InlineData("Сборка App.slnx успешна за 3 с.", "успешно", false)]
    [InlineData("Сборка App.slnx успешна за 3 с, предупреждений: 2.", "успешно, предупреждений: 2", false)]
    [InlineData("Сборка App.slnx не удалась за 4 с: ошибок 3, предупреждений 0.\na.cs(1,1): error CS1: x", "ошибок: 3", true)]
    [InlineData("Сборка App.slnx прервана по тайм-ауту через 600 с.", "тайм-аут", true)]
    public void Build_DetailFromReport(string result, string detail, bool failure)
    {
        var view = Present(BuildAgentTools.BuildName, result);

        Assert.Equal(("Сборка", detail, failure), (view.Title, view.Detail, view.IsFailure));
    }

    [Theory]
    [InlineData("Тесты (App.Tests): успешно 12 из 12, не прошло 0, пропущено 0 за 2 с.", "прошло 12 из 12", false)]
    [InlineData("Тесты (App.Tests): успешно 10 из 12, не прошло 2, пропущено 0 за 2 с.\nfail", "не прошло 2 из 12", true)]
    [InlineData("Тесты (App.Tests) не запустились: ошибок сборки 1.", "не собрались", true)]
    public void Tests_DetailFromReport(string result, string detail, bool failure)
    {
        var view = _presenter.Present(new AgentToolCall(BuildAgentTools.RunTestsName, new Dictionary<string, object?> { ["filter"] = "Order" }, result))!;

        Assert.Equal(("Тесты «Order»", AgentToolIcon.Test, detail, failure), (view.Title, view.Icon, view.Detail, view.IsFailure));
    }

    [Theory]
    [InlineData("Код выхода 0 за 1 с.\nOn branch main", "код 0", false)]
    [InlineData("Код выхода 1 за 1 с. Вывода нет.", "код 1", true)]
    [InlineData("Команда прервана по тайм-ауту через 120 с.", "тайм-аут", true)]
    public void Command_ExitCode(string result, string detail, bool failure)
    {
        var view = _presenter.Present(new AgentToolCall(CommandAgentTools.RunCommandName, new Dictionary<string, object?> { ["command"] = "git status" }, result))!;

        Assert.Equal(("Команда git status", detail, failure), (view.Title, view.Detail, view.IsFailure));
    }

    // Feed rows parse reports via resource formats, so they understand any UI language.
    [Fact]
    public void Reports_ReadInEnglish()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var error = new BuildDiagnostic("a.cs", 1, 1, IsError: true, "CS1", "x");
            var failedBuild = BuildReportText.Summary(new BuildReport("App.slnx", false, [error, error], TimeSpan.FromSeconds(2.5), DateTimeOffset.Now));
            var passedTests = TestReportText.Summary(new TestReport("App.Tests", 12, 12, 0, 0, [], [], Parsed: true) { Elapsed = TimeSpan.FromSeconds(2) });

            var build = Present(BuildAgentTools.BuildName, failedBuild);

            Assert.Equal(("Build", "errors: 2", true), (build.Title, build.Detail, build.IsFailure));
            Assert.Equal("12 of 12 passed", Present(BuildAgentTools.RunTestsName, passedTests).Detail);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Running_HasNoDetail() => Assert.Equal(("Сборка…", null), (Present(BuildAgentTools.BuildName, null).Title, Present(BuildAgentTools.BuildName, null).Detail));

    private AgentToolView Present(string name, string? result) => _presenter.Present(new AgentToolCall(name, new Dictionary<string, object?>(), result))!;
}
