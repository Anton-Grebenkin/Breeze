using System.ClientModel;
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
/// A request that fails before the first text or call is repeated with growing pauses (retrying after content would
/// duplicate work and cost): a stream error ("temporarily rate-limited upstream", a stream cut off mid-reasoning), an
/// answer 429 or 5xx left after the library's own quick retries, a dropped connection. An evening of 502s then costs
/// a minute of waiting, not the turn. Each pause is reported to <see cref="RequestRetries"/>.
/// </para>
/// </summary>
/// <param name="retryDelays">Pauses before retries; their count is the number of retries.</param>
/// <param name="retries">Where a pause is reported; <c>null</c> for nowhere.</param>
/// <param name="describe">The failure in the user's words, for the report.</param>
internal sealed class StreamFailureChatClient(
    IChatClient inner, IReadOnlyList<TimeSpan>? retryDelays = null, RequestRetries? retries = null, Func<Exception, string>? describe = null) : DelegatingChatClient(inner)
{
    private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)];

    private const int StreamErrorRetries = 2;
    private const int RequestTimeout = 408;
    private const int TooEarly = 425;
    private const int TooManyRequests = 429;
    private const int ServerErrors = 500;

    private readonly IReadOnlyList<TimeSpan> _retryDelays = retryDelays ?? DefaultRetryDelays;

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var started = false;
            Exception? failure = null;
            await using (var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                while (failure is null)
                {
                    ChatResponseUpdate? update;
                    try
                    {
                        update = await updates.MoveNextAsync() ? updates.Current : null;
                    }
                    catch (Exception error) when (!started && IsTransient(error, cancellationToken) && attempt < RetriesAfter(error))
                    {
                        failure = error;
                        break;
                    }

                    if (update is null)
                    {
                        yield break;
                    }

                    if (Failure(update) is { } text)
                    {
                        failure = new ModelStreamException(text);
                    }
                    else
                    {
                        // Service events (response start) and reasoning do not block a retry: with no text or calls
                        // yet, no work is duplicated; at most an interrupted reasoning stays in the feed.
                        started |= update.Contents.Any(static content => content is not TextReasoningContent);
                        yield return update;
                    }
                }
            }

            if (started || attempt >= RetriesAfter(failure))
            {
                throw failure;
            }

            retries?.Report(new RequestRetry(describe?.Invoke(failure) ?? failure.Message, _retryDelays[attempt], attempt + 1, RetriesAfter(failure)));
            await Task.Delay(_retryDelays[attempt], cancellationToken);
        }
    }

    // An error inside the stream is often the service's verdict on the request itself (an unsupported field): two
    // quick retries cover the rate limit case without a minute of waiting for an answer that will not change.
    private int RetriesAfter(Exception failure) =>
        failure is ModelStreamException ? Math.Min(StreamErrorRetries, _retryDelays.Count) : _retryDelays.Count;

    /// <summary>Whether repeating the same request may succeed: the service or the network failed, not the request.</summary>
    internal static bool IsTransient(Exception error, CancellationToken cancellationToken) => error switch
    {
        ModelStreamException => true,

        // Status 0: no answer at all (the connection failed).
        ClientResultException result => result.Status is 0 or RequestTimeout or TooEarly or TooManyRequests or >= ServerErrors,
        HttpRequestException or IOException or TimeoutException => true,

        // An HTTP timeout looks like a cancellation nobody asked for.
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };

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
