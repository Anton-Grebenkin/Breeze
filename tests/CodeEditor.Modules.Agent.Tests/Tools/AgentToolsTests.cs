using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Tools;

public sealed class AgentToolsTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ProviderTools_ReachModel_AndToolErrorsAreExplained()
    {
        _fixture.ToolProviders.Add(new Provider());
        _fixture.Client
            .CallTool("c1", "read_file", new Dictionary<string, object?> { ["path"] = "../secret.txt" })
            .Reply("Не могу прочитать этот файл.");

        await _fixture.SendAsync("прочитай ../secret.txt");

        Assert.Contains(_fixture.Client.LastOptions!.Tools!, tool => tool.Name == "read_file");
        var result = _fixture.Client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single();
        Assert.Equal("Ошибка: Путь вне рабочей папки.", result.Result?.ToString());
    }

    private sealed class Provider : IAgentToolProvider
    {
        public IEnumerable<AITool> CreateTools() =>
            [AIFunctionFactory.Create(Fail, "read_file")];

        private static string Fail(string path) => throw new AgentToolException("Путь вне рабочей папки.");
    }
}
