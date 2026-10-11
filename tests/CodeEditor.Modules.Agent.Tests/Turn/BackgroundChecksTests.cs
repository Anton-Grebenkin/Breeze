using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Checks that outlive their tool call: a build or test run that went on in the background.</summary>
public sealed class BackgroundChecksTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task RunningJobs_RemindOnce_BeforeTheSummary()
    {
        _fixture.Jobs.Items.Add("#5 `dotnet test`");
        _fixture.Client.Reply("Тесты идут в фоне.").Reply("Всё ещё идут, итог напишу позже.");

        await _fixture.SendAsync("прогони тесты");

        Assert.Equal(2, _fixture.Client.Requests.Count);
        Assert.Contains("#5 `dotnet test`", _fixture.Client.Requests[1].Last(message => message.Role == ChatRole.User).Text, StringComparison.Ordinal);
        Assert.Contains(_fixture.Chat.Messages, message => message is { Kind: ChatMessageKind.Status, Text: "Фоновые прогоны ещё идут — агент ждёт их результатов" });
        Assert.Equal("Всё ещё идут, итог напишу позже.", _fixture.Chat.Messages[^1].Text);
    }

    [Fact]
    public void CheckStartedBeforeAnEdit_DoesNotVerifyIt()
    {
        var file = Path.Combine(AgentFixture.Root, "a.cs");
        _fixture.Gate.Begin();
        var started = _fixture.Gate.StartCheck();
        _fixture.FileState.RecordWrite(file, "sealed class A { }", "class A { }");

        _fixture.Gate.Record(VerificationKind.Build, succeeded: true, started);
        var whileRunning = _fixture.Gate.FindGap(requireTests: false);
        _fixture.Gate.Record(VerificationKind.Build, succeeded: true);

        Assert.Equal(new VerificationGap(VerificationKind.Build, Failed: false), whileRunning);
        Assert.Null(_fixture.Gate.FindGap(requireTests: false));
    }
}
