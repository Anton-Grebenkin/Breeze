using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.Modules.Agent.Tests.Models;

/// <summary>
/// Supported models (ADR 0017): a price/quality set that is the same across services up to naming, with exact context
/// windows from the catalog.
/// </summary>
public sealed class ModelCatalogTests
{
    [Fact]
    public void ServiceLists_StartWithDefaultModel_AndAreSupported()
    {
        Assert.Equal(AgentOptions.DefaultModel, ModelCatalog.ProvodModels[0]);
        Assert.Equal(AgentOptions.DefaultModel, ModelCatalog.ProxyApiModels[0]);
        Assert.Equal(AgentOptions.DefaultModel, ModelCatalog.AitunnelModels[0]);
        Assert.All(ModelCatalog.ProvodModels.Concat(ModelCatalog.ProxyApiModels).Concat(ModelCatalog.AitunnelModels), model => Assert.NotNull(ModelCatalog.SupportedFor(model)));
    }

    // Fast models for simple tasks, optimal for everyday work, a powerful one for hard tasks; six per service.
    [Fact]
    public void Set_IsFastOptimalPowerful_OfFourVendors()
    {
        var tiers = ModelCatalog.ProvodModels.GroupBy(model => ModelCatalog.SupportedFor(model)!.Tier).ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(new Dictionary<ModelTier, int> { [ModelTier.Fast] = 2, [ModelTier.Optimal] = 3, [ModelTier.Powerful] = 1 }, tiers);
        Assert.Equal(["anthropic", "deepseek", "openai", "x-ai"], ModelCatalog.ProvodModels.Select(ModelVendors.VendorOf).Distinct().Order());
        Assert.Equal(ModelCatalog.ProvodModels.Count, ModelCatalog.ProxyApiModels.Count);
        Assert.Equal(ModelCatalog.ProvodModels.Count, ModelCatalog.AitunnelModels.Count);
    }

    // Provod lacks the newest versions, so the previous ones are used: GPT-6 Sol for 6.1, Claude Sonnet 5 for 5.5.
    [Fact]
    public void Provod_TakesPreviousVersion_WhereNewIsMissing()
    {
        Assert.Contains("openai/gpt-6-sol", ModelCatalog.ProvodModels);
        Assert.Contains("anthropic/claude-sonnet-5", ModelCatalog.ProvodModels);
        Assert.Contains("openai/gpt-6.1-sol", ModelCatalog.ProxyApiModels);
        Assert.Contains("anthropic/claude-sonnet-5-5", ModelCatalog.ProxyApiModels);
    }

    [Fact]
    public void Choices_AreModelsOfTheService_CurrentModelKept()
    {
        Assert.Equal(ModelCatalog.AitunnelModels, ModelCatalog.Choices(new AgentOptions { Model = "x-ai/grok-4.7" }));
        Assert.Equal(ModelCatalog.ProvodModels, ModelCatalog.Choices(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = "x-ai/grok-4.7" }));
        Assert.Equal(ModelCatalog.ProxyApiModels, ModelCatalog.Choices(new AgentOptions { Endpoint = AgentServices.ProxyApiEndpoint, Model = "x-ai/grok-4.7" }));
        Assert.Equal("openai/gpt-6-astra", ModelCatalog.Choices(new AgentOptions { Model = "openai/gpt-6-astra" })[0]);
    }

    [Theory]
    [InlineData("x-ai/grok-4.7", null, 500_000)]
    [InlineData("x-ai/grok-4.7", 200_000, 200_000)]
    [InlineData("x-ai/grok-4.3", null, 256_000)]
    [InlineData("deepseek/deepseek-v4.1-flash", null, 1_048_576)]
    public void ContextWindow_IsExactForSupported_SettingWins(string model, int? setting, int expected) =>
        Assert.Equal(expected, ModelCatalog.ContextWindowFor(new AgentOptions { Model = model, ContextWindow = setting }));

    [Fact]
    public void SupportedFor_IgnoresCase_AndUnknownIsNull()
    {
        Assert.Equal(ModelTier.Fast, ModelCatalog.SupportedFor("DeepSeek/DeepSeek-V4.1-Flash")?.Tier);
        Assert.Null(ModelCatalog.SupportedFor("deepseek/deepseek-v4-flash"));
        Assert.Null(ModelCatalog.SupportedFor("xiaomi/mimo-v2.6-pro"));
        Assert.Null(ModelCatalog.SupportedFor("qwen/qwen3-coder-plus"));
    }
}
