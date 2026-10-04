using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Raises an error inside the response stream as <see cref="ModelStreamException"/>. A failure sent as a stream event
/// with status 200 (Responses API <c>error</c> and <c>response.failed</c>) reaches us as an update without text: the turn
/// looked like an empty reply, the agent re-asked the model and the user never saw the cause (e.g. Provod rejecting
/// unsupported request fields).
/// <para>
/// A request that fails before the first text or call is retried at most twice with a pause, as Provod and AITUNNEL
/// both advise (retrying after content would duplicate work and cost). Typical cases: "temporarily rate-limited
/// upstream" right after the response starts, or a stream cut off mid-reasoning.
/// </para>
/// </summary>
/// <param name="retryDelays">Pauses before retries; their count is the number of retries.</param>
internal sealed class StreamFailureChatClient(IChatClient inner, IReadOnlyList<TimeSpan>? retryDelays = null) : DelegatingChatClient(inner)
{
    private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    private readonly IReadOnlyList<TimeSpan> _retryDelays = retryDelays ?? DefaultRetryDelays;

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var started = false;
            string? failure = null;
            await using (var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                while (failure is null)
                {
                    ChatResponseUpdate? update;
                    try
                    {
                        update = await updates.MoveNextAsync() ? updates.Current : null;
                    }
                    catch (ModelStreamException error) when (!started && attempt < _retryDelays.Count)
                    {
                        failure = error.Message;
                        break;
                    }

                    if (update is null)
                    {
                        yield break;
                    }

                    failure = Failure(update);
                    if (failure is null)
                    {
                        // Service events (response start) and reasoning do not block a retry: with no text or calls
                        // yet, no work is duplicated; at most an interrupted reasoning stays in the feed.
                        started |= update.Contents.Any(static content => content is not TextReasoningContent);
                        yield return update;
                    }
                }
            }

            if (started || attempt >= _retryDelays.Count)
            {
                throw new ModelStreamException(failure);
            }

            await Task.Delay(_retryDelays[attempt], cancellationToken);
        }
    }

    /// <returns>The error text with its code if the update carries an error; otherwise <c>null</c>.</returns>
    internal static string? Failure(ChatResponseUpdate update)
    {
        if (update.Contents.OfType<ErrorContent>().FirstOrDefault() is { } error)
        {
            return WithCode(error.ErrorCode, error.Message);
        }

        return update.RawRepresentation is StreamingResponseFailedUpdate failed
            ? WithCode(failed.Response.Error?.Code.ToString(), failed.Response.Error?.Message ?? failed.Response.Status?.ToString() ?? string.Empty)
            : null;
    }

    private static string WithCode(string? code, string message) => string.IsNullOrEmpty(code) ? message : $"{message} ({code})";
}
