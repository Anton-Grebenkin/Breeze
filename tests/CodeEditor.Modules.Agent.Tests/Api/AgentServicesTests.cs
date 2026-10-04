using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Modules.Agent.ViewModels.Models;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>Model services with their own keys and models, and switching between them in the model parameters.</summary>
public sealed class AgentServicesTests : IDisposable
{
    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("https://api.proxyapi.ru/v1", AgentSecrets.ApiKey)]
    [InlineData("https://API.provod.ai/v1/", AgentSecrets.ProvodApiKey)]
    [InlineData("https://api.aitunnel.ru/v1", AgentSecrets.AitunnelApiKey)]
    [InlineData("http://localhost:8080/v1", AgentSecrets.ApiKey)]
    public void EachServiceHasItsOwnKey_OtherAddressesUseProxyApiKey(string endpoint, string secret) =>
        Assert.Equal(secret, AgentServices.SecretFor(endpoint));

    // Switching services doesn't erase another service's key.
    [Fact]
    public void Key_FollowsTheService()
    {
        _fixture.Secrets.Remove(AgentSecrets.ProvodApiKey);
        var changes = 0;
        _fixture.ApiKey.Changed += (_, _) => changes++;

        Use(AgentServices.ProvodEndpoint);
        Assert.False(_fixture.ApiKey.HasKey);
        _fixture.ApiKey.Set("sk-provod-test");
        Use(AgentServices.AitunnelEndpoint);

        Assert.True(_fixture.ApiKey.HasKey);
        Assert.Equal("sk-test", _fixture.Secrets.Get(AgentSecrets.AitunnelApiKey));
        Assert.Equal("sk-provod-test", _fixture.Secrets.Get(AgentSecrets.ProvodApiKey));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void Connection_TakesTheKeyOfTheService()
    {
        _fixture.Secrets.Set(AgentSecrets.ProvodApiKey, "sk_live_test");

        var (key, endpoint) = OpenAIChatClientFactory.Connection(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint }, _fixture.Secrets);

        Assert.Equal(("sk_live_test", "api.provod.ai"), (key, endpoint.Host));
    }

    // Services name Claude differently: picking a service sets its default model if it lacks the current one.
    [Fact]
    public async Task ServiceMenu_OfSeveralServices_SwitchWritesAddressAndModelOfTheService()
    {
        _fixture.Options.Set(new AgentOptions { Model = "anthropic/claude-sonnet-5.5" });
        var menu = new ServiceMenu(_fixture.Settings, _fixture.Options, _fixture.StatusBar, [AgentServices.Aitunnel, AgentServices.ProxyApi]);
        var services = Assert.Single(menu.Build());
        Assert.Equal(["AITUNNEL", "ProxyAPI"], services.Items.Select(item => item.Header));
        Assert.True(services.Items[0].IsChecked);

        await services.Items[1].Command!.ExecuteAsync(null);

        Assert.Equal(AgentServices.ProxyApiEndpoint, _fixture.Settings.Written[ServiceMenu.EndpointKey]);
        Assert.Equal("openai/gpt-6-luna", _fixture.Settings.Written[ModelSettingsViewModel.ModelKey]);
    }

    // Only AITUNNEL is selectable, so there is no Service group; the code for other services stays.
    [Fact]
    public void OnlyAitunnelIsSelectable_ServiceGroupHidden()
    {
        Assert.Equal([AgentServices.Aitunnel], AgentServices.Selectable);
        Assert.Equal(AgentServices.AitunnelEndpoint, AgentOptions.DefaultEndpoint);
        Assert.DoesNotContain(_fixture.Chat.Model.ParameterMenu, item => item.Header == "Сервис");
    }

    [Fact]
    public void Choices_AreTheModelsOfTheService()
    {
        var provod = ModelCatalog.Choices(new AgentOptions { Endpoint = AgentServices.ProvodEndpoint, Model = "x-ai/grok-4.7" });

        Assert.Equal(ModelCatalog.ProvodModels, provod);
        Assert.Contains("anthropic/claude-opus-5.5", provod);
        Assert.Equal(ModelTier.Powerful, ModelCatalog.SupportedFor("anthropic/claude-opus-5.5")?.Tier);
    }

    // The key dialog names the current service and links to its site; an own endpoint gets the protocol it needs.
    [Fact]
    public void KeyPrompt_NamesTheService()
    {
        Use(AgentServices.AitunnelEndpoint);
        Assert.Equal("Вставьте API-ключ AITUNNEL.", _fixture.ApiKey.KeyPrompt());
        Assert.Equal(new Uri("https://aitunnel.ru/"), _fixture.ApiKey.KeyPage);

        Use("http://localhost:8080/v1");
        Assert.StartsWith("Вставьте API-ключ сервиса localhost (настройка agent.endpoint).", _fixture.ApiKey.KeyPrompt(), StringComparison.Ordinal);
        Assert.Null(_fixture.ApiKey.KeyPage);
    }

    private void Use(string endpoint) => _fixture.Options.Set(new AgentOptions { Endpoint = endpoint, Model = AgentFixture.TestModel });
}
