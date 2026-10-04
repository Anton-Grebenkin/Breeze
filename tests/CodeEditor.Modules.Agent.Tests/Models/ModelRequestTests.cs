using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Models;

/// <summary>Request and prompt tailored to the model family (ADR 0010).</summary>
public sealed class ModelRequestTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("anthropic/claude-sonnet-5", null)]
    [InlineData("openai/gpt-6-luna", null)]
    [InlineData("deepseek/deepseek-v4-flash", 0.3f)]
    [InlineData("google/gemini-3.5-flash", 0.3f)]
    public void Temperature_OnlyForModelsThatAcceptIt(string model, float? expected)
    {
        var options = new AgentOptions { Model = model, Temperature = 0.3 };

        Assert.Equal(expected, ModelRequest.Temperature(options, ModelProfiles.For(model)));
    }

    [Fact]
    public void Gpt_RequestsReasoningSummary_EvenWithDefaultEffort()
    {
        var reasoning = ModelRequest.Reasoning(AgentReasoningEffort.Default, ModelProfiles.For("openai/gpt-6-luna"));

        Assert.Equal(ReasoningOutput.Full, reasoning!.Output);
        Assert.Null(reasoning.Effort);
    }

    [Fact]
    public void Gpt_WithoutReasoning_AsksNoSummary()
    {
        var reasoning = ModelRequest.Reasoning(AgentReasoningEffort.None, ModelProfiles.For("openai/gpt-6-luna"));

        Assert.Equal(ReasoningEffort.None, reasoning!.Effort);
        Assert.Null(reasoning.Output);
    }

    // Native reasoning in reasoning_content defaults to medium effort, or some models don't return it; think-aloud
    // models get no parameter, and explicitly disabled reasoning gets "none".
    [Theory]
    [InlineData("deepseek/deepseek-v4-flash", AgentReasoningEffort.Default, "Medium")]
    [InlineData("deepseek/deepseek-v4-flash", AgentReasoningEffort.High, "High")]
    [InlineData("deepseek/deepseek-v4-flash", AgentReasoningEffort.None, "None")]
    [InlineData("mistralai/devstral-2512", AgentReasoningEffort.Default, null)]
    public void OtherFamilies_ReasoningEffort(string model, AgentReasoningEffort effort, string? expected) =>
        Assert.Equal(expected, ModelRequest.Reasoning(effort, ModelProfiles.For(model))?.Effort?.ToString());

    [Theory]
    [InlineData("openai/gpt-6-luna", "Keep working until the request is fully resolved")]
    [InlineData("anthropic/claude-sonnet-5", "stay within its scope")]
    [InlineData("google/gemini-3.5-flash", "function calling")]
    public async Task LastMessage_CarriesFamilyReminder_AfterContext(string model, string reminder)
    {
        _fixture.Options.Set(new AgentOptions { Model = model });
        _fixture.Client.Reply("Ответ.");

        await _fixture.SendAsync("вопрос");

        var parts = _fixture.Client.Requests[0][^1].Contents.OfType<TextContent>().Select(content => content.Text).ToList();
        Assert.Equal("вопрос", parts[0]);
        Assert.StartsWith("<context>", parts[1], StringComparison.Ordinal);
        Assert.StartsWith("<reminder>", parts[2], StringComparison.Ordinal);
        Assert.Contains(reminder, parts[2], StringComparison.Ordinal);
    }
}
