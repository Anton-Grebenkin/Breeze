using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Conversation;

/// <summary>
/// The agent conversation: an Agent Framework agent and its session with history. The agent is created on the first
/// message (OpenAI and framework assemblies stay unloaded at startup) and recreated after settings or key changes.
/// </summary>
public sealed partial class AgentConversation : IDisposable
{
    public const string AgentName = "CodeEditorAgent";

    private readonly IChatClientFactory _clients;
    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly SystemPrompt _prompt;
    private readonly IEnumerable<IAgentToolProvider> _toolProviders;
    private readonly ILogger<AgentConversation> _logger;
    private readonly IDisposable? _subscription;
    private readonly ApiKeyState _apiKey;
    private readonly TurnBudget _budget;
    private readonly ContextCompaction _compaction;
    private readonly ExploreAgent _explore;
    private readonly ReadOnlyToolSet _readTools;
    private readonly TranscriptRecorder _transcript;
    private readonly DeepSupervisor _deep;
    private readonly PromptCacheMonitor _cache;
    private readonly PromptCacheKey _cacheKey;
    private readonly CacheWarmup _warmups;
    private readonly UserMessageQueue _queue;
    private readonly ProjectRuleGuard? _rules;
    private readonly ContextMeter _meter = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private IChatClient? _client;
    private IChatClient? _summaryClient;
    private CacheWarmupChatClient? _warmup;
    private string _clientSettings;
    private AIAgent? _agent;

    // Kept to save the session after a key change: serializing history does not need a client.
    private AIAgent? _lastAgent;
    private AgentSession? _session;
    private JsonElement? _savedSession;

    public AgentConversation(
        IChatClientFactory clients,
        IOptionsMonitor<AgentOptions> options,
        ApiKeyState apiKey,
        SystemPrompt prompt,
        IEnumerable<IAgentToolProvider> toolProviders,
        TurnBudget budget,
        ContextCompaction compaction,
        ExploreAgent explore,
        ReadOnlyToolSet readTools,
        TranscriptRecorder transcript,
        DeepSupervisor deep,
        PromptCacheKey cacheKey,
        CacheWarmup warmups,
        UserMessageQueue queue,
        ILogger<AgentConversation> logger,
        ProjectRuleGuard? rules = null)
    {
        _rules = rules;
        _cacheKey = cacheKey;
        _warmups = warmups;
        _queue = queue;
        _budget = budget;
        _compaction = compaction;
        _explore = explore;
        _readTools = readTools;
        _transcript = transcript;
        _deep = deep;
        _clients = clients;
        _options = options;
        _prompt = prompt;
        _toolProviders = toolProviders;
        _logger = logger;
        _cache = new PromptCacheMonitor(logger);
        _clientSettings = AgentClientSettings.Of(options.CurrentValue);
        _subscription = options.OnChange(OnOptionsChanged);
        _apiKey = apiKey;
        _apiKey.Changed += OnApiKeyChanged;
        _prompt.Changed += OnPromptChanged;
    }

    /// <summary>Extra tools (tests); the main ones come from module contributions.</summary>
    public IList<AITool> Tools { get; } = [];

    /// <summary>System prompt and tools the model would get with current settings, without creating the agent.</summary>
    public AgentRequestPreview Preview()
    {
        var options = _options.CurrentValue;
        return new AgentRequestPreview(_prompt.Build(options), CollectTools(ModelProfiles.For(options.Model)));
    }

    /// <summary>Sends a message and streams the response: text, tool calls and their results.</summary>
    /// <exception cref="AgentConfigurationException">The agent is not configured.</exception>
    public async IAsyncEnumerable<AgentResponseUpdate> SendAsync(
        ChatMessage message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_prompt.TakeRulesChanged())
        {
            Invalidate();
        }

        var agent = EnsureAgent();
        if (_session is null)
        {
            // A saved chat continues with the same model history.
            _session = _savedSession is { } saved
                ? await agent.DeserializeSessionAsync(saved, cancellationToken: cancellationToken)
                : await agent.CreateSessionAsync(cancellationToken);
            _savedSession = null;
        }
        await using var updates = agent.RunStreamingAsync(message, _session, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync())
                {
                    SettledApprovals.Remove(_session);
                    yield break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The agent does not record a stopped turn; we do, or the model forgets the question and the calls.
                StoppedTurnHistory.Keep(_session, _transcript.LastRequest, message);
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException && KeepFailedTurn(message))
            {
                // Unreachable: the filter only records history and returns false, so the exception propagates as is.
                throw;
            }

            yield return updates.Current;
        }
    }

    // A turn broken by a service error is recorded like a stopped one; otherwise "continue" starts from scratch.
    private bool KeepFailedTurn(ChatMessage message)
    {
        if (_session is not null)
        {
            StoppedTurnHistory.Keep(_session, _transcript.LastRequest, message, StoppedTurnHistory.FailedNote);
        }

        return false;
    }

    /// <summary>New chat: history is dropped, the agent stays.</summary>
    public void Reset()
    {
        _warmup?.Cancel();
        _session = null;
        _savedSession = null;
        _cache.Reset();
        _cacheKey.Renew();
        _meter.Reset();
        _transcript.Reset();
    }

    /// <summary>Continues a saved chat: the session is restored on the next message.</summary>
    public void Restore(JsonElement? session)
    {
        _warmup?.Cancel();
        _session = null;
        _savedSession = session;
        _cache.Reset();
        _cacheKey.Renew();
        _meter.Reset();
        _transcript.Reset();
    }

    /// <summary>The session for saving the chat; <c>null</c> if there were no messages yet.</summary>
    public async Task<JsonElement?> SerializeSessionAsync(CancellationToken cancellationToken) =>
        _lastAgent is not null && _session is not null
            ? await _lastAgent.SerializeSessionAsync(_session, cancellationToken: cancellationToken)
            : _savedSession;

    /// <summary>Settings or key changed: the next request recreates the client; history is kept.</summary>
    public void Invalidate()
    {
        _client?.Dispose();
        _client = null;
        _warmup = null;
        _summaryClient?.Dispose();
        _summaryClient = null;
        _agent = null;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _apiKey.Changed -= OnApiKeyChanged;
        _prompt.Changed -= OnPromptChanged;
        _client?.Dispose();
        _summaryClient?.Dispose();
        _writeLock.Dispose();
    }

    // Mode and approvals apply per turn: the client stays, and so does a pending cache warm-up.
    private void OnOptionsChanged(AgentOptions changed)
    {
        var settings = AgentClientSettings.Of(changed);
        if (settings != Interlocked.Exchange(ref _clientSettings, settings))
        {
            Invalidate();
        }
    }

    private void OnApiKeyChanged(object? sender, EventArgs e) => Invalidate();

    // Another folder means instructions with its name and path; the conversation history is kept.
    private void OnPromptChanged(object? sender, EventArgs e) => Invalidate();

    private AIAgent EnsureAgent()
    {
        if (_agent is not null)
        {
            return _agent;
        }

        var options = _options.CurrentValue;
        _client = new RecordingChatClient(new BudgetChatClient(CompactingClient(options), _budget), _transcript);
        var profile = ModelProfiles.For(options.Model);
        var tools = CollectTools(profile);
        LogAgentCreated(_logger, options.Model, EndpointHost(options.Endpoint), tools.Count, options.Temperature, options.MaxOutputTokens, options.ReasoningEffort);
        _agent = _client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = new ChatOptions
            {
                Instructions = _prompt.Build(options),
                Tools = tools,
                Temperature = ModelRequest.Temperature(options, profile),
                MaxOutputTokens = options.MaxOutputTokens ?? profile.DefaultMaxOutputTokens,
                Reasoning = ModelRequest.Reasoning(options.ReasoningEffort, profile),
            },
        });
        ConfigureInvocation(_agent);
        _lastAgent = _agent;
        return _agent;
    }

    // Compaction sits under the tool loop and runs before every model request on copies of the messages (its marks
    // must not reach the history). Summaries and cache warmup repeat the request as the model got it.
    private IChatClient CompactingClient(AgentOptions options)
    {
        _summaryClient = _clients.Create(new AgentOptions
        {
            Endpoint = options.Endpoint,
            Model = string.IsNullOrWhiteSpace(options.HelperModel) ? options.Model : options.HelperModel,
        });
        var model = _clients.Create(options);
        _warmup = _warmups.Wrap(model, options);
        var summaries = new CompactionModels(model, () => _cache.LastOptions, _summaryClient);
        return new DetachedMessagesChatClient(new ChatClientBuilder(new LoggingChatClient(_warmup ?? model, options.Model, _cache, _meter, _logger))
            .UseAIContextProviders(_compaction.CreateProvider(options, summaries, _meter))
            .Build());
    }

    // Log only the service host, without the URL path and query.
    private static string EndpointHost(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Authority : "(invalid address)";

    // Reads run in parallel; the iteration cap is a safety net above the turn budget, which stops softly.
    private static void ConfigureInvocation(AIAgent agent)
    {
        if (agent.GetService<FunctionInvokingChatClient>() is { } invoker)
        {
            invoker.AllowConcurrentInvocation = true;
            invoker.MaximumIterationsPerRequest = AgentModes.RequestLimit + 1;
        }
    }

    // Module tools and the scout get a wrapper: logging, model-readable errors, mode and turn budget, retries, one edit
    // at a time. The list is the same in every mode so the request prefix stays cached; mode restrictions apply at call
    // time (ADR 0012). It depends only on the model: the edit tool matches its family.
    private List<AITool> CollectTools(ModelProfile profile)
    {
        List<AITool> tools = [.. Tools.Concat(_toolProviders.SelectMany(provider => provider.CreateTools())).Where(tool => profile.Allows(tool.Name))];
        _readTools.Update(tools);
        tools.Add(_explore.CreateTool());
        return [.. tools.Select(tool => tool is AIFunction function ? new GuardedToolFunction(function, _budget, _writeLock, _logger, _deep, _queue, _rules) : tool)];
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Agent ready: model {Model}, service {Endpoint}, tools {Tools}, temperature {Temperature}, max output tokens {MaxOutputTokens}, reasoning {Reasoning}")]
    private static partial void LogAgentCreated(ILogger logger, string model, string endpoint, int tools, double? temperature, int? maxOutputTokens, AgentReasoningEffort reasoning);
}
