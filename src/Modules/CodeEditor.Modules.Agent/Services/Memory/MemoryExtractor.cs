using System.Text.Json;
using System.Text.Json.Serialization;
using CodeEditor.Modules.Agent.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Memory;

/// <summary>
/// Automatic folder memory (ADR 0012): after a turn, the helper model reads its condensed log and proposes up to three
/// notes, saved to <see cref="ProjectMemory"/> and shown in the feed. A turn without tool calls or corrections is
/// skipped (nothing to remember), as is a turn with a web or browser page (ADR 0029): external text must not become a
/// note. Notes are marked as written by the helper model, so unused ones are forgotten later. A model failure breaks
/// nothing. Disabled by <c>agent.autoMemory</c>.
/// </summary>
public sealed partial class MemoryExtractor(
    IChatClientFactory clients,
    IOptionsMonitor<AgentOptions> options,
    ProjectMemory memory,
    TranscriptRecorder transcript,
    HelperUsage usage,
    TurnBudget budget,
    ILogger<MemoryExtractor> logger)
{
    public const int MaxNotesPerTurn = 3;
    public const int MaxOutputTokens = 4_000;
    public const int MaxDigestCharacters = 24_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// Whether to extract memory from this turn: there were tool calls or it is not the chat's first request, and the
    /// model received no external text (<see cref="TurnBudget.SawExternalContent"/>).
    /// </summary>
    public bool ShouldRun(int toolCalls, int userMessages) =>
        options.CurrentValue.AutoMemory && memory.Folder is not null && (toolCalls > 0 || userMessages > 1) && !budget.SawExternalContent;

    /// <param name="finalAnswer">The turn's final answer; it is not in the recorded model request yet.</param>
    /// <returns>Saved notes; empty if nothing was kept or the model did not answer.</returns>
    public async Task<IReadOnlyList<MemoryNote>> ExtractAsync(string finalAnswer, CancellationToken cancellationToken)
    {
        var agent = options.CurrentValue;
        var settings = new AgentOptions
        {
            Endpoint = agent.Endpoint,
            Api = agent.Api,
            Model = string.IsNullOrWhiteSpace(agent.HelperModel) ? agent.Model : agent.HelperModel,
        };
        try
        {
            var digest = ToolOutput.Limit(TranscriptDigest.Render(transcript.LastRequest) + "\n\nAGENT: " + finalAnswer, MaxDigestCharacters);
            var profile = ModelProfiles.For(settings.Model);
            using var client = new UsageCountingChatClient(clients.Create(settings), usage);
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, MemoryPrompts.Request(memory.IndexForModel() ?? ChatStartContext.EmptyMemory, digest))],
                new ChatOptions
                {
                    Instructions = MemoryPrompts.System,
                    Temperature = ModelRequest.Temperature(settings, profile),
                    MaxOutputTokens = MaxOutputTokens,
                    Reasoning = ModelRequest.Reasoning(AgentReasoningEffort.Low, profile),
                },
                cancellationToken);
            var saved = Save(Parse(SubagentRunner.WithoutThinking(response.Text)));
            LogExtracted(logger, settings.Model, saved.Count);
            return saved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception, settings.Model);
            return [];
        }
    }

    /// <summary>Parses the model's answer: a JSON array, even with text around it; anything unparsable is empty.</summary>
    public static IReadOnlyList<Proposal> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = text.IndexOf('[', StringComparison.Ordinal);
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Proposal>>(text.AsSpan(start, end - start + 1), JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private List<MemoryNote> Save(IReadOnlyList<Proposal> proposals)
    {
        var saved = new List<MemoryNote>();
        foreach (var proposal in proposals.Take(MaxNotesPerTurn))
        {
            if (string.IsNullOrWhiteSpace(proposal.Name) || string.IsNullOrWhiteSpace(proposal.Content))
            {
                continue;
            }

            try
            {
                var type = (proposal.Type ?? "project").Trim().ToLowerInvariant();
                memory.Save(proposal.Name.Trim(), type, proposal.Description ?? string.Empty, proposal.Content, automatic: true);
                saved.Add(memory.Read(proposal.Name.Trim())!);
            }
            catch (AgentToolException exception)
            {
                LogRejected(logger, proposal.Name, exception.Message);
            }
        }

        return saved;
    }

    /// <summary>A note as the model proposes it.</summary>
    public sealed record Proposal(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("content")] string? Content);

    [LoggerMessage(Level = LogLevel.Information, Message = "Memory extraction on {Model}: {Saved} notes saved")]
    private static partial void LogExtracted(ILogger logger, string model, int saved);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Memory extraction on {Model} failed; nothing saved")]
    private static partial void LogFailed(ILogger logger, Exception exception, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Memory note {Name} rejected: {Reason}")]
    private static partial void LogRejected(ILogger logger, string name, string reason);
}
