using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Client for an OpenAI-compatible API at the configured endpoint: the services (<see cref="AgentServices"/>) expose
/// models of many vendors in this format, so no other connector is needed. Protocol per <see cref="AgentApi"/>: OpenAI
/// models use the Responses API when the service supports it (GPT-6+ cannot call tools while reasoning via Chat
/// Completions), Claude uses Anthropic Messages when the dialect says so (<see cref="AnthropicMessagesChatClient"/>),
/// the rest use Chat Completions. Fields missing from the library come from model and dialect together
/// (<see cref="RequestExtensions"/>, ADR 0021). One conversation key per chat (<see cref="PromptCacheKey"/>).
/// </summary>
public sealed class OpenAIChatClientFactory(ISecretStore secrets, PromptCacheKey cacheKey, UserDataPaths paths, TimeProvider time) : IChatClientFactory
{
    public const string OpenAIVendorPrefix = "openai/";
    public const string AnthropicVendorPrefix = "anthropic/";
    public const string XaiVendorPrefix = "x-ai/";

    /// <summary>xAI header: requests with the same value go to the same server, where their cache lives.</summary>
    public const string GrokConversationHeader = "x-grok-conv-id";

    public IChatClient Create(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var (key, endpoint) = Connection(options, secrets);
        var clientOptions = ClientOptions(options, endpoint, () => cacheKey.Current);
        if (TrafficLogPolicy.For(options, paths, time) is { } traffic)
        {
            clientOptions.AddPolicy(traffic, PipelinePosition.PerTry);
        }

        return Create(options, new ApiKeyCredential(key), clientOptions);
    }

    /// <summary>API key and endpoint, shared by chat and the model list.</summary>
    /// <exception cref="AgentConfigurationException">No API key, or the endpoint is not a URL.</exception>
    public static (string Key, Uri Endpoint) Connection(AgentOptions options, ISecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        var key = secrets.Get(AgentServices.SecretFor(options.Endpoint))
            ?? throw new AgentConfigurationException(Strings.ErrorNoApiKey);
        return Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            ? (key, endpoint)
            : throw new AgentConfigurationException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorEndpointNotUrl, options.Endpoint));
    }

    /// <summary>Client over the given transport options: tests plug in their own transport and see the request body.</summary>
    /// <param name="streamRetryDelays">Failed stream retry pauses (<see cref="StreamFailureChatClient"/>); <c>null</c> for defaults.</param>
    internal static IChatClient Create(AgentOptions options, ApiKeyCredential key, OpenAIClientOptions clientOptions, IReadOnlyList<TimeSpan>? streamRetryDelays = null)
    {
        var dialect = AgentServices.DialectFor(options.Endpoint);

        // Some services want the id without the vendor; routing decisions and the profile use the full id.
        var model = dialect.ShortModelIds ? ModelVendors.ShortName(options.Model) : options.Model;
        IChatClient chat;
        if (UsesMessages(options))
        {
            var endpoint = clientOptions.Endpoint ?? throw new ArgumentException("The client options have no endpoint.", nameof(clientOptions));
            chat = new AnthropicMessagesChatClient(AnthropicMessagesChatClient.Pipeline(key, clientOptions), AnthropicMessagesChatClient.MessagesUri(endpoint), model);
        }
        else
        {
            var client = new OpenAIClient(key, clientOptions);
            chat = UsesResponses(options)
                ? new StatelessResponsesChatClient(client.GetResponsesClient().AsIChatClient(model), dialect.EncryptedReasoning)
                : client.GetChatClient(model).AsIChatClient();
        }

        chat = new StreamFailureChatClient(chat, streamRetryDelays);
        return new EmptyContentFilterChatClient(dialect.EmptyReplyPlaceholder ? new ProxyPlaceholderChatClient(chat) : chat);
    }

    /// <param name="conversationKey">Conversation key for the provider cache (<see cref="PromptCacheKey"/>); <c>null</c> for none.</param>
    internal static OpenAIClientOptions ClientOptions(AgentOptions options, Uri endpoint, Func<string>? conversationKey = null)
    {
        var dialect = AgentServices.DialectFor(options.Endpoint);
        var clientOptions = new OpenAIClientOptions { Endpoint = endpoint };
        clientOptions.AddPolicy(new ErrorBodyPolicy(), PipelinePosition.PerCall);
        var edits = RequestExtensions.For(options, dialect, conversationKey);
        if (edits.Count > 0)
        {
            // All edits share one body parse; each runs even if an earlier one already changed something.
            clientOptions.AddPolicy(new JsonBodyPolicy(body => edits.Aggregate(false, (changed, edit) => edit(body) | changed)), PipelinePosition.PerCall);
        }

        if (!UsesResponses(options) && !UsesMessages(options))
        {
            var thinksAloud = ModelProfiles.For(options.Model).Reasoning == ReasoningSource.ThinkAloud;
            clientOptions.AddPolicy(new ReasoningFieldPolicy(keepReasoning: !thinksAloud), PipelinePosition.PerCall);
        }

        if (conversationKey is not null && RequestExtensions.UsesGrokSessionKey(options, dialect))
        {
            clientOptions.AddPolicy(new HeaderPolicy(GrokConversationHeader, conversationKey), PipelinePosition.PerCall);
        }

        return clientOptions;
    }

    /// <summary>Claude goes via Anthropic Messages when the service dialect says so and the protocol setting is automatic.</summary>
    public static bool UsesMessages(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Api == AgentApi.Auto
            && options.Model.StartsWith(AnthropicVendorPrefix, StringComparison.OrdinalIgnoreCase)
            && AgentServices.DialectFor(options.Endpoint).ClaudeMessages;
    }

    public static bool UsesResponses(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Api switch
        {
            AgentApi.Responses => true,
            AgentApi.ChatCompletions => false,
            _ => (options.Model.StartsWith(OpenAIVendorPrefix, StringComparison.OrdinalIgnoreCase)
                    || options.Endpoint.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
                && AgentServices.DialectFor(options.Endpoint).OpenAIProtocol == AgentApi.Responses,
        };
    }
}
