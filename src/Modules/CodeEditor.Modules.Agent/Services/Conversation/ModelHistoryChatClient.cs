using System.Runtime.CompilerServices;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// Keeps the history valid for the model it goes to, so a chat can switch models and services midway:
/// - reasoning goes back only to the model and service that wrote it: encrypted GPT reasoning and Claude's signed
///   thinking are rejected by any other model or service account ("invalid signature", "could not decrypt"). Each
///   reasoning part of a response is stamped with its origin; older parts go by their look (GPT's are Fernet tokens);
/// - a model without vision gets a note instead of the history's images, which it would reject.
/// Requests carry adapted copies: the history keeps everything, so switching back brings the reasoning back.
/// </summary>
internal sealed class ModelHistoryChatClient(IChatClient inner, string endpoint, string model) : DelegatingChatClient(inner)
{
    public const string OriginKey = "breeze.origin";

    // OpenAI's encrypted reasoning (Responses API) is a Fernet token, which always starts so.
    private const string FernetPrefix = "gAAAAA";

    private readonly string _origin = Origin(endpoint, model);
    private readonly ModelProfile _profile = ModelProfiles.For(model);

    /// <summary>Who wrote a reasoning part: the service host and the model.</summary>
    public static string Origin(string endpoint, string model) =>
        (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Authority.ToUpperInvariant() : endpoint) + "/" + model;

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(Adapt(messages), options, cancellationToken);
        foreach (var message in response.Messages)
        {
            Stamp(message.Contents);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(Adapt(messages), options, cancellationToken))
        {
            Stamp(update.Contents);
            yield return update;
        }
    }

    // The parts of one reasoning merge into one, which keeps the first part's properties: every part gets the stamp.
    private void Stamp(IList<AIContent> contents)
    {
        foreach (var reasoning in contents.OfType<TextReasoningContent>())
        {
            (reasoning.AdditionalProperties ??= [])[OriginKey] = _origin;
        }
    }

    private List<ChatMessage> Adapt(IEnumerable<ChatMessage> messages) => [.. messages.Select(Adapt)];

    private ChatMessage Adapt(ChatMessage message)
    {
        if (!message.Contents.Any(content => IsForeignReasoning(content) || IsUnseenImage(content)))
        {
            return message;
        }

        var copy = message.Clone();
        copy.Contents = [.. message.Contents.Where(content => !IsForeignReasoning(content)).Select(content => IsUnseenImage(content) ? new TextContent(Strings.ImageNotForModel) : content)];
        return copy;
    }

    private bool IsForeignReasoning(AIContent content) => content is TextReasoningContent reasoning && !IsOwn(reasoning);

    private bool IsOwn(TextReasoningContent reasoning) =>
        reasoning.AdditionalProperties?.TryGetValue(OriginKey, out var origin) == true
            ? origin?.ToString() == _origin
            : reasoning.ProtectedData is { Length: > 0 } data && IsSigned(data);

    // Reasoning from before the stamps: GPT's tokens go to GPT models, other signatures (Claude's) to Claude models.
    private bool IsSigned(string data) => _profile.Family switch
    {
        ModelFamily.Gpt or ModelFamily.GptReasoning => data.StartsWith(FernetPrefix, StringComparison.Ordinal),
        ModelFamily.Claude => !data.StartsWith(FernetPrefix, StringComparison.Ordinal),
        _ => false,
    };

    private bool IsUnseenImage(AIContent content) => !_profile.SeesImages && content switch
    {
        DataContent data => data.HasTopLevelMediaType("image"),
        UriContent uri => uri.HasTopLevelMediaType("image"),
        _ => false,
    };
}
