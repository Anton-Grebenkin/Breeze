using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Terminal.Services.Commands;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Tests;

public sealed class CommandAgentToolsTests : IDisposable
{
    private readonly TerminalFixture _fixture = new();
    private readonly CommandAgentTools _tools;
    private readonly AIFunction _run;

    public CommandAgentToolsTests()
    {
        _tools = new CommandAgentTools(_fixture.Workspace, _fixture.FileSystem, _fixture.Output, _fixture.SaveBeforeRun, _fixture.Outputs, _fixture.Background, _fixture.Activity, _fixture.Shell, _fixture.Blocks);
        _run = _tools.CreateTools().OfType<AIFunction>().Single();
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void RunCommand_RequiresApproval() => Assert.IsType<ApprovalRequiredAIFunction>(_run);

    [Fact]
    public async Task Preview_ShowsCommandAndFolder()
    {
        var previews = await _tools.PreviewAsync(CommandAgentTools.RunCommandName, new Dictionary<string, object?> { ["command"] = "git status", ["cwd"] = "src" }, TestContext.Current.CancellationToken);

        var preview = Assert.Single(previews);
        Assert.Equal((ProposedChangeKind.Command, "src", "git status"), (preview.Kind, preview.RelativePath, preview.NewText));
    }

    [Fact]
    public async Task Preview_OfForbiddenCommand_Fails() =>
        await Assert.ThrowsAsync<AgentToolException>(() => _tools.PreviewAsync(CommandAgentTools.RunCommandName, new Dictionary<string, object?> { ["command"] = "shutdown /s" }, TestContext.Current.CancellationToken));

    // A command finished within the wait gives a normal result and leaves nothing in the background (ADR 0026).
    [Fact]
    public async Task Run_UsesPowerShell_InFolder_FinishedWithinTheWait()
    {
        _fixture.Runner.Returns(0, "On branch main");

        var result = await Invoke(new() { ["command"] = "git status", ["cwd"] = "src", ["waitSeconds"] = 5000 });

        var request = Assert.Single(_fixture.Runner.Requests);
        Assert.Equal("powershell.exe", request.FileName);
        Assert.Equal("-Command", request.Arguments[^2]);
        Assert.Equal(PowerShellLauncher.Script("git status"), request.Arguments[^1]);
        Assert.True(request.HideSecretVariables);
        Assert.Equal(TerminalFixture.PathOf("src"), request.WorkingDirectory);
        Assert.StartsWith("Код выхода 0 за ", result, StringComparison.Ordinal);
        Assert.EndsWith("\nOn branch main", result, StringComparison.Ordinal);
        Assert.Equal((result, CommandAgentTools.RunCommandName), Assert.Single(_fixture.Outputs.Calls));
        Assert.Empty(_fixture.Background.All);
    }

    // Still running after the wait: continues in the background with its id and output so far.
    [Fact]
    public async Task Run_StillRunningAfterTheWait_ContinuesInBackground()
    {
        _fixture.Runner.Hold = new TaskCompletionSource();
        _fixture.Runner.Returns(0, "Building…");

        var result = await Invoke(new() { ["command"] = "npm run build", ["waitSeconds"] = 0 });

        Assert.StartsWith("Фоновая команда #1 идёт", result, StringComparison.Ordinal);
        Assert.Contains("Building…", result, StringComparison.Ordinal);
        Assert.True(_fixture.Background.Get(1).IsRunning);
        _fixture.Runner.Hold.SetResult();
    }

    // Antivirus refused the wrapped script (nothing ran): the command goes again bare, and stays bare afterwards.
    [Fact]
    public async Task Run_BlockedByAntivirus_RepeatsWithoutTheWrapper_AndRemembersIt()
    {
        _fixture.Runner
            .Returns(1, "At line:1 char:1\n+ FullyQualifiedErrorId : " + PowerShellLauncher.BlockedMarker)
            .Returns(0, "On branch main")
            .Returns(0, "v1");

        var first = await Invoke(new() { ["command"] = "git status", ["waitSeconds"] = 5000 });
        var second = await Invoke(new() { ["command"] = "git --version", ["waitSeconds"] = 5000 });

        Assert.Equal(["git status", "git --version"], _fixture.Runner.Requests.Skip(1).Select(request => request.Arguments[^1]));
        Assert.EndsWith("\nOn branch main", first, StringComparison.Ordinal);
        Assert.DoesNotContain(PowerShellLauncher.BlockedMarker, first, StringComparison.Ordinal);
        Assert.EndsWith("\nv1", second, StringComparison.Ordinal);
        Assert.Equal(PowerShellWrapping.Bare, _fixture.Shell.Wrapping);
        Assert.True(_fixture.FileSystem.FileExists(Path.Combine(TerminalFixture.UserData, PowerShellMode.FileName)));
    }

    // Blocked even bare: the model gets the reason in plain words, the second block in a row switches the tool off,
    // and the user is told once.
    [Fact]
    public async Task Run_BlockedEvenBare_ExplainsIt_AndSwitchesTheToolOffOnTheSecondBlock()
    {
        _fixture.Runner
            .Returns(1, PowerShellLauncher.BlockedMarker)
            .Returns(1, "bare: " + PowerShellLauncher.BlockedMarker)
            .Returns(1, "bare: " + PowerShellLauncher.BlockedMarker);

        var first = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(new() { ["command"] = "git status", ["waitSeconds"] = 5000 }));
        var second = await Assert.ThrowsAsync<AgentToolException>(() => Invoke(new() { ["command"] = "echo hello", ["waitSeconds"] = 5000 }));

        Assert.Equal(3, _fixture.Runner.Requests.Count);
        Assert.StartsWith("Команда не выполнялась: антивирус", first.Message, StringComparison.Ordinal);
        Assert.EndsWith("bare: " + PowerShellLauncher.BlockedMarker, first.Message, StringComparison.Ordinal);
        Assert.Equal((false, true), (first.ToolUnavailable, second.ToolUnavailable));
        Assert.Equal(CommandBlocks.NotificationId, Assert.Single(_fixture.Notifications.Shown).Id);
    }

    [Fact]
    public async Task Run_OutsideWorkspace_IsRejected() =>
        Assert.Contains("вне рабочей папки", (await Assert.ThrowsAsync<AgentToolException>(() => Invoke(new() { ["command"] = "dir", ["cwd"] = @"C:\Windows" }))).Message, StringComparison.Ordinal);

    private async Task<string?> Invoke(Dictionary<string, object?> arguments) =>
        (await _run.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString();
}
