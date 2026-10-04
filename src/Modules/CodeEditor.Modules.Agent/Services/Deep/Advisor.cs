using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Deep mode advisor (ADR 0012): a second model the harness consults at fixed points, before the first edit and on
/// stuck signals. It sees a condensed work log (<see cref="TranscriptDigest"/>) and the current situation, and answers
/// briefly. An advisor failure does not break the turn: there is just no advice.
/// </summary>
public sealed partial class Advisor(
    IChatClientFactory clients,
    IOptionsMonitor<AgentOptions> options,
    TranscriptRecorder transcript,
    HelperUsage usage,
    ILogger<Advisor> logger)
{
    /// <summary>Room for reasoning: reasoning models count it toward the output limit, while the advice is 150 words.</summary>
    public const int MaxOutputTokens = 12_000;

    /// <summary>Advisor and reviewer model: <c>agent.advisorModel</c>, otherwise the agent model.</summary>
    public static AgentOptions Settings(AgentOptions agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return new AgentOptions
        {
            Endpoint = agent.Endpoint,
            Api = agent.Api,
            Model = string.IsNullOrWhiteSpace(agent.AdvisorModel) ? agent.Model : agent.AdvisorModel,
        };
    }

    /// <returns>The advice, or <c>null</c> if the advisor did not answer.</returns>
    public async Task<string?> ConsultAsync(AdvisorCheckpoint checkpoint, string focus, CancellationToken cancellationToken)
    {
        var agent = options.CurrentValue;
        var settings = Settings(agent);
        var profile = ModelProfiles.For(settings.Model);
        try
        {
            using var client = new UsageCountingChatClient(clients.Create(settings), usage);
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, AdvisorPrompts.Request(TranscriptDigest.Render(transcript.LastRequest), focus))],
                new ChatOptions
                {
                    Instructions = AdvisorPrompts.System(checkpoint),
                    Temperature = ModelRequest.Temperature(settings, profile),
                    MaxOutputTokens = MaxOutputTokens,
                    Reasoning = ModelRequest.Reasoning(agent.AdvisorReasoningEffort, profile),
                },
                cancellationToken);
            var advice = SubagentRunner.WithoutThinking(response.Text);
            LogConsulted(logger, checkpoint, settings.Model, advice.Length);
            return advice.Length == 0 ? null : advice;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, exception, settings.Model);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Advisor {Checkpoint} on {Model}: {Length} characters")]
    private static partial void LogConsulted(ILogger logger, AdvisorCheckpoint checkpoint, string model, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Advisor {Model} is unavailable, the checkpoint is skipped")]
    private static partial void LogFailed(ILogger logger, Exception exception, string model);
}
