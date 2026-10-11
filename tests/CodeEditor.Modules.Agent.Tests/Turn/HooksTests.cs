using System.Text.Json;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Services.Hooks;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// The user's hooks, as in Claude Code: a command gets the event as JSON; exit code 2 blocks a call, comments on a
/// result, stops a message or keeps the turn going; other codes don't stop the agent.
/// </summary>
public sealed class HooksTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private int _runs;

    public HooksTests() =>
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string path) => "ok " + path + Interlocked.Increment(ref _runs), "probe"));

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task PreToolUse_ExitCode2_BlocksTheCall_AndTellsTheModelWhy()
    {
        UseHooks(new AgentHookSettings { PreToolUse = [new AgentHook { Matcher = "probe|run_command", Command = "check.ps1" }] });
        _fixture.Processes.Returns(HookRunner.BlockExitCode, "Нельзя трогать миграции");
        _fixture.Client.CallTool("c1", "probe", new Dictionary<string, object?> { ["path"] = "db/migrations" }).Reply("Понял.");

        await _fixture.SendAsync("поправь");

        Assert.Equal(0, _runs);
        Assert.Contains("Нельзя трогать миграции", ToolResult(), StringComparison.Ordinal);
        var request = Assert.Single(_fixture.Processes.Requests);
        Assert.Equal(HookEvents.PreToolUse, request.Environment["BREEZE_HOOK_EVENT"]);
        using var input = JsonDocument.Parse(request.Input!);
        Assert.Equal("probe", input.RootElement.GetProperty("tool_name").GetString());
        Assert.Equal("db/migrations", input.RootElement.GetProperty("tool_input").GetProperty("path").GetString());
    }

    // The matcher names whole tools; a hook that fails with another code doesn't stop the agent.
    [Fact]
    public async Task OtherToolsAndFailingHooks_DoNotStopTheCall()
    {
        UseHooks(new AgentHookSettings { PreToolUse = [new AgentHook { Matcher = "prob", Command = "never.ps1" }, new AgentHook { Command = "broken.ps1" }] });
        _fixture.Processes.Returns(1, "ParserError");
        _fixture.Client.CallTool("c1", "probe", new Dictionary<string, object?> { ["path"] = "a" }).Reply("Готово.");

        await _fixture.SendAsync("прочитай");

        Assert.Equal(1, _runs);
        Assert.Contains("broken.ps1", Assert.Single(_fixture.Processes.Requests).Arguments[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostToolUse_ExitCode2_AddsItsWordToTheResult()
    {
        UseHooks(new AgentHookSettings { PostToolUse = [new AgentHook { Command = "lint.ps1" }] });
        _fixture.Processes.Returns(HookRunner.BlockExitCode, "lint: 3 предупреждения");
        _fixture.Client.CallTool("c1", "probe", new Dictionary<string, object?> { ["path"] = "a" }).Reply("Готово.");

        await _fixture.SendAsync("прочитай");

        Assert.StartsWith("ok a1", ToolResult(), StringComparison.Ordinal);
        Assert.Contains("lint: 3 предупреждения", ToolResult(), StringComparison.Ordinal);
        using var input = JsonDocument.Parse(Assert.Single(_fixture.Processes.Requests).Input!);
        Assert.Equal("ok a1", input.RootElement.GetProperty("tool_response").GetString());
    }

    [Fact]
    public async Task UserPromptSubmit_AddsContext_OrStopsTheMessage()
    {
        UseHooks(new AgentHookSettings { UserPromptSubmit = [new AgentHook { Command = "context.ps1" }] });
        _fixture.Processes.Returns(0, "Ветка: feature/x").Returns(HookRunner.BlockExitCode, "Сначала закоммить изменения");
        _fixture.Client.Reply("Ок.");

        await _fixture.SendAsync("привет");
        await _fixture.SendAsync("ещё");

        Assert.Contains("<hook_context>\nВетка: feature/x\n</hook_context>", _fixture.Client.Requests[0][^1].Text, StringComparison.Ordinal);
        Assert.Single(_fixture.Client.Requests);
        Assert.Contains(_fixture.Chat.Messages, message => message.Kind == ChatMessageKind.Error && message.Text.Contains("Сначала закоммить изменения", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stop_ExitCode2_KeepsTheTurnGoing_AtMostThreeTimes()
    {
        UseHooks(new AgentHookSettings { Stop = [new AgentHook { Command = "tests.ps1" }] });
        for (var run = 0; run < 5; run++)
        {
            _fixture.Processes.Returns(HookRunner.BlockExitCode, "Тесты не прошли");
        }

        _fixture.Client.Reply("Готово.").Reply("Исправил.").Reply("Ещё раз.").Reply("Всё.");

        await _fixture.SendAsync("сделай");

        Assert.Equal(1 + TurnChecks.MaxStopHooks, _fixture.Client.Requests.Count);
        Assert.Contains("Тесты не прошли", _fixture.Client.Requests[1].Last(message => message.Role == ChatRole.User).Text, StringComparison.Ordinal);
        Assert.Equal(TurnChecks.MaxStopHooks, _fixture.Processes.Requests.Count);
        using var second = JsonDocument.Parse(_fixture.Processes.Requests[1].Input!);
        Assert.True(second.RootElement.GetProperty("stop_hook_active").GetBoolean());
    }

    // A hook runs any command: a cloned repository's .breeze/settings.json must not add one.
    [Theory]
    [InlineData("agent:hooks")]
    [InlineData("Agent:Hooks:PreToolUse:0:Command")]
    public void FolderSettings_CannotSetHooks(string key)
    {
        var services = new ServiceCollection();

        new AgentModule().ConfigureServices(services);

        Assert.Contains(services.Select(static service => service.ImplementationInstance).OfType<UserOnlySetting>(), setting => setting.Covers(key));
    }

    private void UseHooks(AgentHookSettings hooks) =>
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Hooks = hooks });

    private string ToolResult() =>
        _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result?.ToString() ?? string.Empty;
}
