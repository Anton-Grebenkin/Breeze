using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json.Nodes;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Agent.Tests.Api;

/// <summary>
/// The user's own connections (ADR 0055): <c>work:team-coder</c> leads to the connection's address, key and headers;
/// a model the user described gets the profile of what it really is, whatever the gateway calls it.
/// </summary>
public sealed class ModelRoutesTests
{
    private static readonly AgentConnection Work = new()
    {
        Id = "work",
        Name = "Рабочий шлюз",
        Endpoint = "https://gateway.test/v1",
        Headers = { ["X-Team"] = "shop" },
        Models =
        {
            new ModelDescription
            {
                Id = "hosted_vllm/team-pro", Name = "DeepSeek, старшая", Family = ModelFamily.DeepSeek,
                ContextWindow = 64_000, MaxOutputTokens = 8_000, Images = true,
            },
        },
    };

    private static AgentOptions Options(string model) => new() { Model = model, Connections = { Work } };

    [Fact]
    public void ReferenceWithAConnection_LeadsToItsAddressKeyAndDescription()
    {
        var route = ModelRoutes.For(Options("work:hosted_vllm/team-pro"));

        Assert.Equal(("hosted_vllm/team-pro", "https://gateway.test/v1", "agent-api-key-connection-work"), (route.Model, route.Endpoint, route.SecretName));
        Assert.Equal(("DeepSeek, старшая", "Рабочий шлюз"), (route.Title, route.ServiceTitle));
        Assert.Equal("shop", route.Headers["X-Team"]);
        Assert.Null(route.Service);
        Assert.Equal(ServiceDialect.Standard, route.Dialect);
    }

    [Theory]
    [InlineData("openai/gpt-6-luna", "openai/gpt-6-luna")]
    [InlineData("qwen3:8b", "qwen3:8b")]
    [InlineData("other:model", "other:model")]
    [InlineData(":model", ":model")]
    public void ReferenceWithoutAKnownConnection_IsAModelOfTheServiceAtTheEndpoint(string reference, string model)
    {
        var route = ModelRoutes.For(Options(reference));

        Assert.Equal((model, AgentOptions.DefaultEndpoint, AgentSecrets.AitunnelApiKey), (route.Model, route.Endpoint, route.SecretName));
        Assert.Equal((AgentServices.Aitunnel, "AITUNNEL"), (route.Service, route.ServiceTitle));
        Assert.Null(route.Connection);
    }

    [Fact]
    public void UndescribedModelOfAConnection_Works_GuessedByItsId()
    {
        var options = Options("WORK:deepseek-chat");

        Assert.Equal(("deepseek-chat", "deepseek-chat"), (ModelRoutes.For(options).Model, ModelRoutes.For(options).Title));
        Assert.Equal(ModelFamily.DeepSeek, ModelProfiles.For(options).Family);
    }

    // The gateway's own name says nothing: by name the model would be "other" with a 128k window and no images.
    [Fact]
    public void DescribedModel_GetsTheProfileOfWhatItIs()
    {
        var options = Options("work:hosted_vllm/team-pro");

        var profile = ModelProfiles.For(options);

        Assert.Equal(ModelFamily.Other, ModelProfiles.For("hosted_vllm/team-pro").Family);
        Assert.Equal((ModelFamily.DeepSeek, FamilyPrompts.Frontier, true, 8_000), (profile.Family, profile.Guidance, profile.SeesImages, profile.DefaultMaxOutputTokens));
        Assert.Equal(64_000, ModelCatalog.ContextWindowFor(options));
    }

    [Fact]
    public void Description_MaySetReasoningAndEditFormat_TheRestStaysGuessed()
    {
        var connection = new AgentConnection
        {
            Id = "lab", Endpoint = "http://localhost:1234/v1",
            Models = { new ModelDescription { Id = "big", Family = ModelFamily.Qwen, Reasoning = ReasoningSource.ThinkAloud, EditFormat = EditFormat.Patch } },
        };

        var profile = ModelProfiles.For(new AgentOptions { Model = "lab:big", Connections = { connection } });

        Assert.Equal((ModelFamily.Qwen, ReasoningSource.ThinkAloud, EditFormat.Patch, false), (profile.Family, profile.Reasoning, profile.EditFormat, profile.SeesImages));
        Assert.Null(profile.DefaultMaxOutputTokens);
    }

    [Fact]
    public async Task Request_GoesToTheConnection_WithItsHeaders_AndTheModelIdOfTheService()
    {
        var options = Options("work:hosted_vllm/team-pro");
        var secrets = new AgentFixture.MemorySecrets();
        secrets.Set("agent-api-key-connection-work", "work-key");
        var (key, endpoint) = OpenAIChatClientFactory.Connection(options, secrets);

        var request = await CaptureAsync(options, key, endpoint);

        Assert.Equal(("work-key", new Uri("https://gateway.test/v1/chat/completions")), (key, request.Uri));
        Assert.Equal("shop", request.Headers["X-Team"]);
        Assert.Equal("hosted_vllm/team-pro", (string?)request.Body["model"]);
        Assert.Equal(100, (int?)request.Body["max_completion_tokens"]);
    }

    [Fact]
    public async Task LegacyMaxTokens_SendsTheLimitAsMaxTokens()
    {
        var connection = new AgentConnection { Id = "old", Endpoint = "https://old.test/v1", Keyless = true, LegacyMaxTokens = true };
        var options = new AgentOptions { Model = "old:llama3", Connections = { connection } };

        // A connection without a key works with an empty secret store.
        var (key, endpoint) = OpenAIChatClientFactory.Connection(options, new AgentFixture.MemorySecrets());
        var request = await CaptureAsync(options, key, endpoint);

        Assert.Equal(100, (int?)request.Body["max_tokens"]);
        Assert.Null(request.Body["max_completion_tokens"]);
    }

    [Fact]
    public void ConnectionWithoutAKey_AsksForOne_UnlessItNeedsNone()
    {
        var secrets = new AgentFixture.MemorySecrets();

        Assert.Throws<AgentConfigurationException>(() => OpenAIChatClientFactory.Connection(Options("work:any"), secrets));

        // The key of another service does not fit.
        secrets.Set(AgentSecrets.AitunnelApiKey, "aitunnel-key");
        Assert.Throws<AgentConfigurationException>(() => OpenAIChatClientFactory.Connection(Options("work:any"), secrets));
    }

    [Theory]
    [InlineData(AgentApi.Auto, "team-pro", false, false)]
    [InlineData(AgentApi.Messages, "team-claude", true, false)]
    [InlineData(AgentApi.Responses, "team-gpt", false, true)]
    [InlineData(AgentApi.Auto, "openai/gpt-6-luna", false, true)]
    public void Protocol_IsTheConnections(AgentApi api, string model, bool messages, bool responses)
    {
        var connection = new AgentConnection { Id = "c", Endpoint = "https://c.test/v1", Api = api };
        var options = new AgentOptions { Model = "c:" + model, Connections = { connection } };

        Assert.Equal((messages, responses), (OpenAIChatClientFactory.UsesMessages(options), OpenAIChatClientFactory.UsesResponses(options)));
    }

    // A helper, the scout or the search may live on another connection than the conversation.
    [Fact]
    public void AnotherModelOfTheSameSettings_KeepsTheConnections()
    {
        var options = Options("openai/gpt-6-luna");

        var helper = options.ForModel("work:hosted_vllm/team-pro");

        Assert.Equal("https://gateway.test/v1", ModelRoutes.For(helper).Endpoint);
        Assert.Equal("https://gateway.test/v1", ModelRoutes.For(options, "work:hosted_vllm/team-pro").Endpoint);
        Assert.Equal(AgentOptions.DefaultEndpoint, ModelRoutes.For(options).Endpoint);
    }

    [Fact]
    public void KeyState_FollowsTheConnection_AndNamesIt()
    {
        var secrets = new AgentFixture.MemorySecrets();
        var options = new TestOptionsMonitor<AgentOptions>(Options("openai/gpt-6-luna"));
        using var state = new ApiKeyState(secrets, options);
        var changes = 0;
        state.Changed += (_, _) => changes++;
        secrets.Set(AgentSecrets.AitunnelApiKey, "aitunnel-key");
        Assert.True(state.HasKey);

        options.Set(Options("work:hosted_vllm/team-pro"));

        Assert.Equal((1, false, null), (changes, state.HasKey, state.Service));
        Assert.Contains("Рабочий шлюз", state.KeyPrompt(), StringComparison.Ordinal);
        state.Set("work-key");
        Assert.Equal(("work-key", true), (secrets.Get("agent-api-key-connection-work"), state.HasKey));
    }

    [Fact]
    public void Pickers_OfferTheDescribedModelsOfConnectionsFirst()
    {
        var choices = ModelCatalog.Choices(Options("openai/gpt-6-luna"));

        Assert.Equal("work:hosted_vllm/team-pro", choices[0]);
        Assert.Contains("openai/gpt-6-luna", choices);
    }

    [Fact]
    public void Settings_BindConnectionsWithModels()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["agent:model"] = "work:team-pro",
            ["agent:connections:0:id"] = "work",
            ["agent:connections:0:endpoint"] = "https://gateway.test/v1",
            ["agent:connections:0:api"] = "chatCompletions",
            ["agent:connections:0:headers:X-Team"] = "shop",
            ["agent:connections:0:models:0:id"] = "team-pro",
            ["agent:connections:0:models:0:family"] = "deepseek",
            ["agent:connections:0:models:0:contextWindow"] = "64000",
            ["agent:connections:0:models:0:reasoning"] = "thinkAloud",
        }).Build();
        var options = new AgentOptions();

        configuration.GetSection(AgentOptions.Section).Bind(options);

        var route = ModelRoutes.For(options);
        Assert.Equal((AgentApi.ChatCompletions, "shop"), (route.Api, route.Headers["x-team"]));
        Assert.Equal((ModelFamily.DeepSeek, 64_000, ReasoningSource.ThinkAloud), (route.Description!.Family, route.Description.ContextWindow, route.Description.Reasoning));
    }

    // A connection sends the whole conversation to its address: a cloned repository must not add one.
    [Theory]
    [InlineData("agent:connections")]
    [InlineData("Agent:Connections:0:Endpoint")]
    public void FolderSettings_CannotSetConnections(string key)
    {
        var services = new ServiceCollection();

        new AgentModule().ConfigureServices(services);

        Assert.Contains(services.Select(static service => service.ImplementationInstance).OfType<UserOnlySetting>(), setting => setting.Covers(key));
    }

    private static async Task<(Uri Uri, IReadOnlyDictionary<string, string> Headers, JsonNode Body)> CaptureAsync(AgentOptions options, string key, Uri endpoint)
    {
        var handler = new CapturingHandler();
        var clientOptions = OpenAIChatClientFactory.ClientOptions(options, endpoint);
        clientOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
        clientOptions.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        using var client = OpenAIChatClientFactory.Create(options, new ApiKeyCredential(key), clientOptions);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "вопрос")], new ChatOptions { MaxOutputTokens = 100 }));
        return (handler.Uri!, handler.Headers, JsonNode.Parse(handler.Body!)!);
    }

    /// <summary>Records the request and fails, since the tests don't need a model answer.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(",", header.Value);
            }

            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"test"}}""") };
        }
    }
}
