using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Commands;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// Agent background commands (ADR 0012): numbered start, new output since the last read, waiting for a pattern,
/// stopping, the concurrency limit, stopping all on folder change, feed and context rows.
/// </summary>
public sealed class BackgroundCommandsTests : IDisposable
{
    private readonly TerminalFixture _fixture = new();
    private readonly AIFunction _run;
    private readonly Dictionary<string, AIFunction> _tools;

    public BackgroundCommandsTests()
    {
        _run = new CommandAgentTools(_fixture.Workspace, _fixture.FileSystem, _fixture.Output, _fixture.SaveBeforeRun, _fixture.Outputs, _fixture.Background)
            .CreateTools().OfType<AIFunction>().Single();
        _tools = new BackgroundCommandTools(_fixture.Background, _fixture.Outputs).CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Background_ReturnsId_ThenNewOutputOnly_ThenExit()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        _fixture.Runner.Returns(0, "Now listening on http://localhost:5000");

        var started = await Invoke(_run, new() { ["command"] = "dotnet run", ["waitSeconds"] = 0 });

        Assert.StartsWith("Фоновая команда #1 идёт", started, StringComparison.Ordinal);
        Assert.Contains("command_output(id: 1)", started, StringComparison.Ordinal);
        Assert.Contains("Now listening", started, StringComparison.Ordinal);
        Assert.Contains("(нового вывода нет)", await Invoke(_tools[BackgroundCommandTools.CommandOutputName], new() { ["id"] = 1 }), StringComparison.Ordinal);

        _fixture.Runner.Hold.SetResult();
        var finished = await Invoke(_tools[BackgroundCommandTools.CommandOutputName], new() { ["id"] = 1, ["timeoutSeconds"] = 10 });

        Assert.StartsWith("Фоновая команда #1 завершилась с кодом 0", finished, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stop_KillsTheCommand_AndReportsIt()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        await Invoke(_run, new() { ["command"] = "npm run watch", ["waitSeconds"] = 0 });

        var stopped = await Invoke(_tools[BackgroundCommandTools.StopCommandName], new() { ["id"] = 1 });

        Assert.StartsWith("Фоновая команда #1 остановлена", stopped, StringComparison.Ordinal);
        Assert.False(_fixture.Background.Get(1).IsRunning);
    }

    [Fact]
    public async Task AtMostFourRunning_AndUnknownIdIsExplained()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        for (var index = 0; index < BackgroundCommands.MaxRunning; index++)
        {
            await Invoke(_run, new() { ["command"] = $"dotnet run --project p{index}", ["waitSeconds"] = 0 });
        }

        var limit = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(_run, new() { ["command"] = "dotnet run", ["waitSeconds"] = 0 }));
        var unknown = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(_tools[BackgroundCommandTools.CommandOutputName], new() { ["id"] = 9 }));

        Assert.Contains("уже идут 4", limit.Message, StringComparison.Ordinal);
        Assert.Equal("Фоновой команды #9 нет.", unknown.Message);
    }

    [Fact]
    public async Task OtherFolder_StopsAllBackgroundCommands()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        await Invoke(_run, new() { ["command"] = "dotnet run", ["waitSeconds"] = 0 });
        var command = _fixture.Background.Get(1);

        _fixture.FileSystem.AddDirectory(Path.GetFullPath(@"C:\other"));
        _fixture.Workspace.Open(Path.GetFullPath(@"C:\other"));
        await command.WaitAsync(pattern: null, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(command.IsStopped);
    }

    [Fact]
    public async Task ContextAndFeed_ShowBackgroundState()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        var started = await Invoke(_run, new() { ["command"] = "dotnet run", ["waitSeconds"] = 0 });

        var context = await new BackgroundCommandsContext(_fixture.Background).GetContextAsync(new AgentContextRequest(IncludeActiveEditor: false), TestContext.Current.CancellationToken);
        var presenter = new TerminalToolPresenter();
        var runRow = presenter.Present(new AgentToolCall(CommandAgentTools.RunCommandName, new Dictionary<string, object?> { ["command"] = "dotnet run" }, started))!;
        var outputRow = presenter.Present(new AgentToolCall(BackgroundCommandTools.CommandOutputName, new Dictionary<string, object?> { ["id"] = 1 }, started))!;

        Assert.StartsWith("Фоновая команда #1 идёт", Assert.Single(context), StringComparison.Ordinal);
        Assert.EndsWith("`dotnet run`", context[0], StringComparison.Ordinal);
        Assert.Equal(("в фоне #1", false), (runRow.Detail, runRow.IsFailure));
        Assert.Equal(("Вывод фоновой команды #1", "идёт"), (outputRow.Title, outputRow.Detail));
    }

    // Real PowerShell: waiting for a pattern, then stopping with the process tree.
    [Fact]
    public async Task RealProcess_WaitForPattern_ThenStop()
    {
        using var workspace = new Workspace(new FakeFileSystem(), new ContextKeyService(), NullLogger<Workspace>.Instance);
        using var commands = new BackgroundCommands(new ProcessRunner(), workspace, TimeProvider.System, NullLogger<BackgroundCommands>.Instance);
        var request = PowerShellLauncher.Request("Write-Output 'готов'; Start-Sleep -Seconds 60", Path.GetTempPath(), TimeSpan.FromSeconds(60));

        var command = commands.Start(request, "сервер", _ => { });
        var matched = await command.WaitAsync("гото", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        commands.Stop(command.Id);
        await command.WaitAsync(pattern: null, TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        Assert.True(matched);
        Assert.Contains("готов", command.ReadNew(), StringComparison.Ordinal);
        Assert.True(command.IsStopped);
    }

    private static async Task<string> Invoke(AIFunction tool, Dictionary<string, object?> arguments) =>
        (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
}
