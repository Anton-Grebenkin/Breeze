using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Services.Hooks;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// A real Windows PowerShell hook: the event's JSON arrives in UTF-8, the reason may come on either output stream, and
/// the UTF-8 prelude adds nothing to what the model reads.
/// </summary>
public sealed class HookRunnerTests
{
    [Theory]
    [InlineData("Write-Output ('Нельзя: ' + $e.prompt)")]
    [InlineData("[Console]::Error.WriteLine('Нельзя: ' + $e.prompt)")]
    public async Task PowerShellHook_ReadsTheEvent_AndBlocksWithItsReason(string reply)
    {
        var hook = new AgentHook { Command = "$e = [Console]::In.ReadToEnd() | ConvertFrom-Json; " + reply + "; exit 2" };
        var runner = Runner(new AgentHookSettings { UserPromptSubmit = [hook] });

        var outcome = await runner.RunAsync(HookEvents.UserPromptSubmit, tool: null, HookInputs.Prompt("удали миграции"), TestContext.Current.CancellationToken);

        Assert.True(outcome.Blocked);
        Assert.Equal("Нельзя: удали миграции", outcome.Message);
    }

    [Fact]
    public async Task PowerShellHook_ExitCode0_OutputIsContext()
    {
        var runner = Runner(new AgentHookSettings { UserPromptSubmit = [new AgentHook { Command = "Write-Output \"Событие: $env:BREEZE_HOOK_EVENT\"" }] });

        var outcome = await runner.RunAsync(HookEvents.UserPromptSubmit, tool: null, HookInputs.Prompt("привет"), TestContext.Current.CancellationToken);

        Assert.False(outcome.Blocked);
        Assert.Equal("Событие: UserPromptSubmit", outcome.Message);
    }

    // No folder is open: the hook runs in the temp folder.
    private static HookRunner Runner(AgentHookSettings hooks) => new(
        new TestOptionsMonitor<AgentOptions>(new AgentOptions { Model = AgentFixture.TestModel, Hooks = hooks }),
        new ProcessRunner(),
        new Workspace(new FakeFileSystem(), new ContextKeyService(), NullLogger<Workspace>.Instance),
        NullLogger<HookRunner>.Instance);
}
