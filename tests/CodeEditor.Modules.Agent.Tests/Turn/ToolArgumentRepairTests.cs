using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts.Text;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>
/// Arguments that don't match the schema: an array sent as a JSON string is parsed, any other mismatch gets a clear
/// error instead of "Function failed" (after which models deleted and recreated the file).
/// </summary>
public sealed class ToolArgumentRepairTests : IDisposable
{
    private readonly AgentFixture _fixture = new();
    private readonly List<string> _received = [];

    public ToolArgumentRepairTests() =>
        _fixture.Conversation.Tools.Add(AIFunctionFactory.Create((string[] items, int count) =>
        {
            _received.Add(string.Join("+", items) + "#" + count);
            return "ok";
        }, "take_items"));

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Repair_ParsesJsonStringForArrayParameter_LeavesOthers()
    {
        var function = AIFunctionFactory.Create((string[] items, string note) => note, "f");
        var arguments = new AIFunctionArguments(new Dictionary<string, object?> { ["items"] = "[\"a\", \"b\"]", ["note"] = "[not an array param]" });

        var repaired = ToolArgumentRepair.Repair(arguments, function.JsonSchema);

        Assert.Equal(["items"], repaired);
        Assert.Equal(JsonValueKind.Array, Assert.IsType<JsonElement>(arguments["items"]).ValueKind);
        Assert.Equal("[not an array param]", arguments["note"]);
    }

    // Brackets inside JSON strings don't count; an extra closing one is dropped, missing ones are appended.
    [Theory]
    [InlineData("""[{"a": "x}"}}]""", """[{"a": "x}"}]""")]
    [InlineData("""[{"a": "q\"}"}]""", """[{"a": "q\"}"}]""")]
    [InlineData("""[{"a": 1]""", """[{"a": 1}]""")]
    [InlineData("""{"a": ["x\\"]""", """{"a": ["x\\"]}""")]
    [InlineData("""[{"a": "обрыв""", """[{"a": "обрыв"}]""")]
    public void BracketRepair_BalancesOutsideStrings(string json, string expected) =>
        Assert.Equal(expected, JsonBracketRepair.Balance(json));

    // Guards against an edit array sent as a string with an extra "}" at the end (seen from Claude Haiku).
    [Fact]
    public void Repair_JsonStringWithExtraBrace_IsParsed()
    {
        var function = AIFunctionFactory.Create((string[] items) => items.Length, "f");
        var arguments = new AIFunctionArguments(new Dictionary<string, object?> { ["items"] = """["a", "b"}]""" });

        Assert.Equal(["items"], ToolArgumentRepair.Repair(arguments, function.JsonSchema));
        Assert.Equal(2, Assert.IsType<JsonElement>(arguments["items"]).GetArrayLength());
    }

    [Fact]
    public async Task ArrayAsJsonString_IsAccepted_EndToEnd()
    {
        _fixture.Client
            .CallTool("c1", "take_items", new Dictionary<string, object?> { ["items"] = "[\"a\", \"b\"]", ["count"] = 2 })
            .Reply("Готово.");

        await _fixture.SendAsync("возьми");

        Assert.Equal(["a+b#2"], _received);
    }

    [Fact]
    public async Task WrongArgumentType_IsExplained_NotFunctionFailed()
    {
        _fixture.Client
            .CallTool("c1", "take_items", new Dictionary<string, object?> { ["items"] = "a, b", ["count"] = "много" })
            .Reply("Понял.");

        await _fixture.SendAsync("возьми");

        var result = _fixture.Client.Requests[^1].SelectMany(message => message.Contents).OfType<FunctionResultContent>().Single().Result?.ToString();
        Assert.StartsWith("Ошибка: Не выполнено: аргументы не подходят к параметрам инструмента", result, StringComparison.Ordinal);
        Assert.Contains("items: array, count: integer", result, StringComparison.Ordinal);
        Assert.Empty(_received);
    }
}
