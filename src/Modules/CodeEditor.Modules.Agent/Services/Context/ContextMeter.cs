namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Request size in tokens: the Agent Framework history estimate, corrected by the model's reported usage. The framework
/// estimates bytes/4 of all message parts: Cyrillic text comes out more expensive than the model counts it, reasoning
/// counts even when it is not sent (Chat Completions), and encrypted GPT reasoning counts by its base64 length. The
/// estimate could be twice the real input, so compaction ran too early and broke the cache each time. The correction
/// is the ratio of the last request's real input (with system prompt and tool schemas) to its history estimate; before
/// the first answer the estimate is used as is.
/// </summary>
public sealed class ContextMeter
{
    /// <summary>Correction bounds: early in a chat the input is mostly the system prompt and says little about history.</summary>
    public const double MinRatio = 0.25;

    public const double MaxRatio = 1.5;

    private readonly Lock _lock = new();
    private int _sent;
    private double _ratio = 1;
    private long _lastInput;
    private int _lastSent;

    /// <summary>
    /// A request is about to go to the model: its size in tokens, for the context ring. Counted from the last request's
    /// real input plus what the history gained or lost since (a tool result, a compaction): the ratio alone is bounded
    /// and would undercount a young chat, where the system prompt is most of the input. Raised on the request's thread.
    /// </summary>
    public event EventHandler<long>? Estimated;

    /// <summary>History estimate of the request being sent to the model (after compaction).</summary>
    public void Sent(int estimate)
    {
        long tokens;
        lock (_lock)
        {
            _sent = estimate;
            tokens = _lastInput > 0 ? Math.Max(0, _lastInput + (long)((estimate - _lastSent) * _ratio)) : (long)(estimate * _ratio);
        }

        Estimated?.Invoke(this, tokens);
    }

    /// <summary>The request's input tokens as reported by the model.</summary>
    public void Observed(long inputTokens)
    {
        lock (_lock)
        {
            if (_sent > 0 && inputTokens > 0)
            {
                _ratio = Math.Clamp(inputTokens / (double)_sent, MinRatio, MaxRatio);
                _lastInput = inputTokens;
                _lastSent = _sent;
            }
        }
    }

    /// <summary>Request tokens for a history with this estimate.</summary>
    public int Tokens(int estimate)
    {
        lock (_lock)
        {
            return (int)(estimate * _ratio);
        }
    }

    /// <summary>
    /// New or opened chat: the correction starts over. Another model keeps it: tokenizers differ by a tenth or two,
    /// while starting over would drop the system prompt from the estimate.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _sent = 0;
            _ratio = 1;
            _lastInput = 0;
            _lastSent = 0;
        }
    }
}
