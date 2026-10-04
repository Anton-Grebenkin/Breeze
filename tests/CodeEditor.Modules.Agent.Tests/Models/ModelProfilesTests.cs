using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.Modules.Agent.Tests.Models;

public sealed class ModelProfilesTests
{
    [Theory]
    [InlineData("anthropic/claude-sonnet-4-6", ModelFamily.Claude, false)]
    [InlineData("anthropic/claude-3-7-sonnet-thinking", ModelFamily.Claude, true)]
    [InlineData("openai/gpt-4.1", ModelFamily.Gpt, false)]
    [InlineData("openai/gpt-5-mini", ModelFamily.GptReasoning, true)]
    [InlineData("openai/o3-mini", ModelFamily.GptReasoning, true)]
    [InlineData("openai/gpt-6-luna", ModelFamily.GptReasoning, true)]
    [InlineData("openai/gpt-5.6-sol", ModelFamily.GptReasoning, true)]
    [InlineData("google/gemini-2.5-pro", ModelFamily.Gemini, false)]
    [InlineData("deepseek/deepseek-chat", ModelFamily.DeepSeek, false)]
    [InlineData("deepseek/deepseek-v4-flash", ModelFamily.DeepSeek, false)]
    [InlineData("deepseek/deepseek-reasoner", ModelFamily.DeepSeek, true)]
    [InlineData("qwen/qwen3-coder", ModelFamily.Frontier, false)]
    [InlineData("x-ai/grok-4.7", ModelFamily.Frontier, false)]
    [InlineData("moonshotai/kimi-k2.7-code", ModelFamily.Frontier, false)]
    [InlineData("llama3", ModelFamily.Other, false)]
    public void Family_AndReasoning_AreDetectedByName(string model, ModelFamily family, bool isReasoning)
    {
        var profile = ModelProfiles.For(model);

        Assert.Equal(family, profile.Family);
        Assert.Equal(isReasoning, profile.IsReasoning);
        Assert.NotEmpty(profile.Guidance);
    }

    [Fact]
    public void Guidance_CompensatesFamilyWeakness()
    {
        Assert.Contains("never write a tool call as text", ModelProfiles.For("google/gemini-2.5-flash").Guidance, StringComparison.Ordinal);
        Assert.Contains("Stop exploring as soon as", ModelProfiles.For("anthropic/claude-opus-4-6").Guidance, StringComparison.Ordinal);
        Assert.Contains("keep working until the request is fully resolved", ModelProfiles.For("openai/gpt-4o").Guidance, StringComparison.Ordinal);
        // Strong models of other vendors batch calls; a "one step at a time" rule made them do 1–2 calls per request.
        Assert.Contains("Batch independent calls", ModelProfiles.For("x-ai/grok-4.7").Guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("One step at a time", ModelProfiles.For("x-ai/grok-4.7").Reminder, StringComparison.Ordinal);
        Assert.Contains("step by step", ModelProfiles.For("llama3").Guidance, StringComparison.Ordinal);
    }

    // From ProxyAPI probes: which models have native reasoning and which must be asked to think aloud.
    [Theory]
    [InlineData("openai/gpt-6-luna", ReasoningSource.Summary)]
    [InlineData("anthropic/claude-sonnet-5", ReasoningSource.Thinking)]
    [InlineData("deepseek/deepseek-v4-flash", ReasoningSource.Native)]
    [InlineData("moonshotai/kimi-k2.7-code", ReasoningSource.Native)]
    [InlineData("google/gemini-3.8-flash", ReasoningSource.Native)]
    [InlineData("x-ai/grok-4.7", ReasoningSource.Native)]
    [InlineData("qwen/qwen3-coder-plus", ReasoningSource.ThinkAloud)]
    [InlineData("mistralai/devstral-2512", ReasoningSource.ThinkAloud)]
    [InlineData("meta-llama/llama-4-maverick", ReasoningSource.ThinkAloud)]
    [InlineData("deepseek/deepseek-chat", ReasoningSource.ThinkAloud)]
    [InlineData("openai/gpt-4.1", ReasoningSource.ThinkAloud)]
    public void Reasoning_OwnOrThinkAloud(string model, ReasoningSource source) =>
        Assert.Equal(source, ModelProfiles.For(model).Reasoning);

    [Fact]
    public void ThinkAloud_InPromptAndReminder_UnlessReasoningOff()
    {
        var devstral = new AgentOptions { Model = "mistralai/devstral-2512" };
        var off = new AgentOptions { Model = "mistralai/devstral-2512", ReasoningEffort = AgentReasoningEffort.None };

        Assert.True(ModelRequest.ThinksAloud(devstral));
        Assert.False(ModelRequest.ThinksAloud(off));
        Assert.False(ModelRequest.ThinksAloud(new AgentOptions { Model = "deepseek/deepseek-v4-flash" }));
    }

    [Fact]
    public void OwnReasoning_RequestedAtMediumByDefault_SoProviderShowsIt()
    {
        var native = ModelRequest.Reasoning(AgentReasoningEffort.Default, ModelProfiles.For("google/gemini-3.8-flash"));
        var aloud = ModelRequest.Reasoning(AgentReasoningEffort.Default, ModelProfiles.For("qwen/qwen3-coder-plus"));

        Assert.Equal(Microsoft.Extensions.AI.ReasoningEffort.Medium, native?.Effort);
        Assert.Null(aloud);
    }
}
