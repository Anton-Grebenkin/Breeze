using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts.Files;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Subagent loop for the explorer or critic (ADR 0012): a short context of its own, wrapped read tools with their own
/// budget (the last request is a summary without tools), usage counted as helper usage of the chat. Subagent reads do
/// not count for the agent: it reads files itself before editing. The answer is the model's last reply without
/// reasoning tags.
/// </summary>
public sealed partial class SubagentRunner(IChatClientFactory clients, AgentFileState fileState, HelperUsage usage, ILogger<SubagentRunner> logger)
{
    /// <summary>One run's settings: model, instructions, reasoning effort, request limit.</summary>
    public sealed record Run(AgentOptions Model, string Instructions, AgentReasoningEffort Reasoning, int MaxRequests);

    public async Task<string> RunAsync(Run run, string request, IReadOnlyList<AIFunction> tools, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(tools);
        var budget = new TurnBudget();
        budget.Begin(AgentMode.Ask, run.MaxRequests, reflectsOnExploration: false);
        using var isolation = fileState.IsolateReads();
        using var writeLock = new SemaphoreSlim(1, 1);
        var profile = ModelProfiles.For(run.Model.Model);
        using var client = new FunctionInvokingChatClient(new BudgetChatClient(new UsageCountingChatClient(clients.Create(run.Model), usage), budget))
        {
            AllowConcurrentInvocation = true,
            MaximumIterationsPerRequest = run.MaxRequests + 1,
        };
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, request)], new ChatOptions
        {
            Instructions = run.Instructions,
            Tools = [.. tools.Select(tool => (AITool)new GuardedToolFunction(tool, budget, writeLock, logger))],
            Temperature = ModelRequest.Temperature(run.Model, profile),
            MaxOutputTokens = profile.DefaultMaxOutputTokens,
            Reasoning = ModelRequest.Reasoning(run.Reasoning, profile),
        }, cancellationToken);
        var answer = WithoutThinking(response.Messages.LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text ?? string.Empty);
        LogFinished(logger, run.Model.Model, budget.Requests, answer.Length);
        return answer;
    }

    /// <summary>Strips inline reasoning (<c>&lt;thinking&gt;</c>, <c>&lt;think&gt;</c> of open models).</summary>
    public static string WithoutThinking(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ThinkingTags().Replace(text, string.Empty).Trim();
    }

    [GeneratedRegex(@"<think(ing)?>[\s\S]*?</think(ing)?>", RegexOptions.CultureInvariant)]
    private static partial Regex ThinkingTags();

    [LoggerMessage(Level = LogLevel.Information, Message = "Subagent on {Model}: {Requests} requests, answer {Length} characters")]
    private static partial void LogFinished(ILogger logger, string model, int requests, int length);
}
