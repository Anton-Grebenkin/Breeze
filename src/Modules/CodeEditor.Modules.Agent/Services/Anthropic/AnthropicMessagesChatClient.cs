using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Anthropic;

/// <summary>
/// Claude via the native Anthropic Messages API (<c>POST …/messages</c>) for services whose OpenAI-format translation
/// corrupts Claude's output (<see cref="ServiceDialect.ClaudeMessages"/>; e.g. lost spaces between call argument chunks
/// when reasoning, ADR 0021). <see cref="AnthropicRequest"/> builds the body and <see cref="AnthropicStreamReader"/>
/// parses the stream. Requests go through a pipeline with the OpenAI client's policies (error body, traffic log,
/// retries) and a transport that tests can replace. Supports one-hour cache warm-up (<see cref="IPromptCacheWarmer"/>).
/// </summary>
internal sealed class AnthropicMessagesChatClient(ClientPipeline pipeline, Uri endpoint, string model) : IChatClient, IPromptCacheWarmer
{
    public const string ApiVersion = "2023-06-01";

    /// <summary>Messages pipeline from the OpenAI client options: the same policies, with the key as a Bearer header.</summary>
    public static ClientPipeline Pipeline(ApiKeyCredential key, ClientPipelineOptions options) => ClientPipeline.Create(
        options,
        [ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(key), new HeaderPolicy("anthropic-version", () => ApiVersion)],
        [],
        []);

    /// <summary>Messages URL next to the OpenAI-compatible one: <c>…/v1</c> → <c>…/v1/messages</c>.</summary>
    public static Uri MessagesUri(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/messages");
    }

    public bool CanWarmUp => ModelProfiles.For(model).Thinking != ClaudeThinking.Budget;

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        await GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        SendAsync(AnthropicRequest.Build(model, messages, options), cancellationToken);

    public async Task<UsageDetails?> WarmUpAsync(IReadOnlyList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var response = await SendAsync(AnthropicRequest.BuildWarmUp(model, messages, options), cancellationToken).ToChatResponseAsync(cancellationToken);
        return response.Usage;
    }

    private async IAsyncEnumerable<ChatResponseUpdate> SendAsync(JsonObject body, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var message = pipeline.CreateMessage();
        message.Apply(new RequestOptions { CancellationToken = cancellationToken });
        message.BufferResponse = false;
        message.Request.Method = "POST";
        message.Request.Uri = endpoint;
        message.Request.Headers.Set("Content-Type", "application/json");
        message.Request.Headers.Set("Accept", "text/event-stream");
        message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
        await pipeline.SendAsync(message);
        var response = message.Response ?? throw new InvalidOperationException("The pipeline returned no response.");
        if (response.IsError)
        {
            throw await ClientResultException.CreateAsync(response);
        }

        var stream = response.ContentStream ?? throw new InvalidOperationException("The Messages response has no content.");
        await foreach (var update in new AnthropicStreamReader(model).ReadAsync(stream, cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }
}
