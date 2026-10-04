using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json.Nodes;
using CodeEditor.Modules.Agent.Services.Anthropic;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// Request fields the library lacks (ADR 0010): GPT message phases in Responses and Claude thinking. Checks the body
/// that would go over the wire: the real client builds the request on a fake transport.
/// </summary>
public sealed class ProviderRequestTests
{
    private static readonly Uri Endpoint = new("https://proxy.test/v1");

    [Fact]
    public async Task Gpt_History_KeepsPhases_PreambleBeforeToolCall_FinalBeforeUser()
    {
        var body = await CaptureAsync("openai/gpt-6-luna", new ChatOptions(),
            new ChatMessage(ChatRole.User, "прочитай notes.txt"),
            new ChatMessage(ChatRole.Assistant, [new TextContent("Читаю notes.txt."), new FunctionCallContent("c1", "read_file", new Dictionary<string, object?> { ["path"] = "notes.txt" })]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("c1", "Level up")]),
            new ChatMessage(ChatRole.Assistant, "Первое слово — палиндром."),
            new ChatMessage(ChatRole.User, "а второе?"));

        var phases = body["input"]!.AsArray().OfType<JsonObject>().Where(item => (string?)item["role"] == "assistant").Select(item => (string?)item["phase"]).ToList();
        Assert.Equal(["commentary", "final_answer"], phases);
    }

    [Fact]
    public void Phases_ConsecutiveRepliesShareNextStep_ExistingPhaseKept()
    {
        var body = JsonNode.Parse("""
            {"input":[
              {"role":"user","content":"x"},
              {"type":"message","role":"assistant","content":"a"},
              {"type":"reasoning","summary":[]},
              {"type":"message","role":"assistant","content":"b"},
              {"type":"function_call","call_id":"c1","name":"f","arguments":"{}"},
              {"type":"function_call_output","call_id":"c1","output":"ok"},
              {"type":"message","role":"assistant","phase":"commentary","content":"c"}]}
            """)!;

        Assert.True(ResponsesPhases.Mark(body));

        var items = body["input"]!.AsArray();
        Assert.Equal(("commentary", "commentary", "commentary"), ((string?)items[1]!["phase"], (string?)items[3]!["phase"], (string?)items[6]!["phase"]));
        Assert.Null(items[0]!["phase"]);
    }

    [Fact]
    public async Task Claude5_GetsAdaptiveThinkingWithSummary_AndCacheMarks()
    {
        var body = await CaptureAsync("anthropic/claude-sonnet-5", new ChatOptions { MaxOutputTokens = 32_000 }, new ChatMessage(ChatRole.User, "вопрос"));

        Assert.Equal("adaptive", (string?)body["thinking"]!["type"]);
        Assert.Equal("summarized", (string?)body["thinking"]!["display"]);
        Assert.Equal("ephemeral", (string?)body["messages"]![0]!["content"]![0]!["cache_control"]!["type"]);
    }

    [Theory]
    [InlineData(null, 4096)]
    [InlineData(ReasoningEffort.High, 8192)]
    public async Task Claude4_GetsThinkingBudget_ByEffort(ReasoningEffort? effort, int budget)
    {
        var options = new ChatOptions { MaxOutputTokens = 32_000, Reasoning = effort is null ? null : new ReasoningOptions { Effort = effort } };

        var body = await CaptureAsync("anthropic/claude-haiku-4-5", options, new ChatMessage(ChatRole.User, "вопрос"));

        Assert.Equal("enabled", (string?)body["thinking"]!["type"]);
        Assert.Equal(budget, (int)body["thinking"]!["budget_tokens"]!);
    }

    [Fact]
    public void Claude4_BudgetLeavesHalfOfAnswer_TooShortAnswer_NoThinking()
    {
        var small = JsonNode.Parse("""{"messages":[],"max_completion_tokens":3000}""")!;
        var tiny = JsonNode.Parse("""{"messages":[],"max_completion_tokens":1500}""")!;

        Assert.True(AnthropicThinking.Enable(small, ClaudeThinking.Budget));
        Assert.Equal(1500, (int)small["thinking"]!["budget_tokens"]!);
        Assert.False(AnthropicThinking.Enable(tiny, ClaudeThinking.Budget));
    }

    [Fact]
    public async Task Claude_ReasoningOff_NoThinking()
    {
        var options = new ChatOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None } };

        var body = await CaptureAsync("anthropic/claude-sonnet-5", options, new ChatMessage(ChatRole.User, "вопрос"));

        Assert.Null(body["thinking"]);
    }

    // An attached image reaches the model as a picture: input_image for GPT, image_url for Claude and Grok.
    [Theory]
    [InlineData("openai/gpt-6-luna", "input_image")]
    [InlineData("anthropic/claude-sonnet-5-5", "image_url")]
    [InlineData("x-ai/grok-4.7", "image_url")]
    public async Task AttachedImage_ReachesTheModelAsPicture(string model, string part)
    {
        var body = await CaptureAsync(model, new ChatOptions(),
            new ChatMessage(ChatRole.User, [new TextContent("что на снимке?"), new DataContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png")]));

        Assert.Contains($"\"type\":\"{part}\"", body.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("anthropic/claude-sonnet-5", ClaudeThinking.Adaptive)]
    [InlineData("anthropic/claude-opus-5-5", ClaudeThinking.Adaptive)]
    [InlineData("anthropic/claude-haiku-4-5", ClaudeThinking.Budget)]
    [InlineData("anthropic/claude-3-7-sonnet", ClaudeThinking.Budget)]
    [InlineData("openai/gpt-6-luna", ClaudeThinking.None)]
    public void Profile_ChoosesClaudeThinking_ByVersion(string model, ClaudeThinking thinking) =>
        Assert.Equal(thinking, ModelProfiles.For(model).Thinking);

    private static async Task<JsonNode> CaptureAsync(string model, ChatOptions chatOptions, params ChatMessage[] messages)
    {
        // Chat Completions fields: ProxyAPI serves Claude this way; AnthropicMessagesTests covers the Messages path.
        var agentOptions = new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = model };
        var handler = new CapturingHandler();
        var clientOptions = OpenAIChatClientFactory.ClientOptions(agentOptions, Endpoint);
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        using var client = OpenAIChatClientFactory.Create(agentOptions, new ApiKeyCredential("test"), clientOptions);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync(messages, chatOptions));
        return JsonNode.Parse(handler.Body!)!;
    }

    /// <summary>Records the request body and fails, since the tests don't need a model answer.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"test"}}""") };
        }
    }
}
