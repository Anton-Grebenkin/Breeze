using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Cache;

/// <summary>
/// Monitors the provider prompt cache (ADR 0012). The request prefix (instructions and tool schemas) stays the same for
/// the whole chat, and each request reads almost all of the previous input from cache since the conversation only
/// grows at the end. Logs prefix changes and cache misses to show what resets the cache (Claude Code counts a miss as
/// more than 5% and 2,000 tokens reprocessed). A new or reopened chat calls <see cref="Reset"/>.
/// </summary>
internal sealed partial class PromptCacheMonitor(ILogger logger)
{
    /// <summary>A miss: more than this many tokens reprocessed and less than half the expected input read from cache.</summary>
    public const int MissThresholdTokens = 2_000;

    private readonly Lock _lock = new();
    private int? _instructions;
    private int? _tools;
    private long _previousInput;
    private ChatOptions? _options;

    /// <summary>
    /// A copy of the chat's last request options: compaction summaries replay the request with them and read it from
    /// cache (<see cref="ConversationSummaryStrategy"/>); <c>null</c> before the first request.
    /// </summary>
    public ChatOptions? LastOptions
    {
        get
        {
            lock (_lock)
            {
                return _options;
            }
        }
    }

    /// <summary>Less than half of the previous input came from cache and a notable amount was reprocessed.</summary>
    /// <param name="expectedCached">Input of the previous request (capped at the current input).</param>
    public static bool IsMiss(long expectedCached, long input, long cached) =>
        expectedCached > 0 && cached < expectedCached / 2 && input - cached > MissThresholdTokens;

    public void Reset()
    {
        lock (_lock)
        {
            _instructions = null;
            _tools = null;
            _previousInput = 0;
            _options = null;
        }
    }

    /// <summary>Before a request: checks whether the prefix changed since the chat's previous request.</summary>
    public void BeforeRequest(ChatOptions? options)
    {
        var instructions = (options?.Instructions ?? string.Empty).GetHashCode(StringComparison.Ordinal);
        var tools = ToolsFingerprint(options?.Tools);
        lock (_lock)
        {
            if (_instructions is { } previous && previous != instructions)
            {
                LogPrefixChanged(logger, "instructions");
            }

            if (_tools is { } previousTools && previousTools != tools)
            {
                LogPrefixChanged(logger, "tools");
            }

            _instructions = instructions;
            _tools = tools;
            _options = options?.Clone();
        }
    }

    /// <summary>After a response: request usage; without cache figures from the service no miss is evaluated.</summary>
    public void AfterResponse(long? input, long? cached)
    {
        if (input is not > 0 || cached is not { } fromCache)
        {
            return;
        }

        lock (_lock)
        {
            var expected = Math.Min(_previousInput, input.Value);
            if (IsMiss(expected, input.Value, fromCache))
            {
                LogCacheMiss(logger, input.Value - fromCache, input.Value, expected);
            }

            _previousInput = input.Value;
        }
    }

    // Name, description and schema of each tool in request order: the order is part of the prefix too.
    private static int ToolsFingerprint(IList<AITool>? tools)
    {
        var hash = new HashCode();
        foreach (var tool in tools ?? [])
        {
            hash.Add(tool.Name, StringComparer.Ordinal);
            hash.Add(tool.Description, StringComparer.Ordinal);
            if (tool is AIFunction function)
            {
                hash.Add(function.JsonSchema.GetRawText(), StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model request prefix changed ({Part}): the provider rebuilds its prompt cache")]
    private static partial void LogPrefixChanged(ILogger logger, string part);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt cache miss: {Uncached} of {Input} input tokens were not cached (about {Expected} expected from cache)")]
    private static partial void LogCacheMiss(ILogger logger, long uncached, long input, long expected);
}
