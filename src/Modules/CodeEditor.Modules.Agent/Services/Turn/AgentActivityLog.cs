using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// User-level agent log: turns with duration and outcome, edit decisions, chats created, opened and deleted.
/// Question and answer text is never logged, only lengths, ids and tool names. Model requests within a turn are logged
/// by <see cref="LoggingChatClient"/>, tool calls by <see cref="GuardedToolFunction"/>.
/// </summary>
public sealed partial class AgentActivityLog(ILogger<AgentActivityLog> logger)
{
    /// <returns>Turn start timestamp for <see cref="TurnFinished"/> and the other outcomes.</returns>
    public long TurnStarted(string? chatId, int questionLength, string? attachment, int files = 0)
    {
        LogTurnStarted(logger, chatId ?? "new", questionLength, attachment ?? "none", files);
        return Stopwatch.GetTimestamp();
    }

    public void TurnFinished(long started, int toolCalls, int approvals) =>
        LogTurnFinished(logger, ElapsedMs(started), toolCalls, approvals);

    public void TurnStopped(long started) => LogTurnStopped(logger, ElapsedMs(started));

    public void TurnFailed(long started, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogTurnFailed(logger, exception, ElapsedMs(started), exception.GetType().Name);
    }

    /// <param name="automatic">The tool is already allowed for this chat, so the card was decided without asking.</param>
    /// <param name="forChat">The user clicked "Allow for this chat".</param>
    public void ApprovalDecided(string tool, int files, bool approved, bool automatic, bool forChat)
    {
        var decision = (approved, automatic, forChat) switch
        {
            (false, _, _) => "rejected",
            (true, true, _) => "applied without asking (allowed for the chat)",
            (true, false, true) => "applied and allowed for the chat",
            _ => "applied",
        };
        LogApproval(logger, tool, files, decision);
    }

    /// <summary>The call ran without a card: a read-only command or a user rule (ADR 0012).</summary>
    public void ApprovedByPolicy(string tool) => LogApproval(logger, tool, 0, "approved without asking (read-only or allowed by a user rule)");

    /// <summary>The user chose "Always allow"; logged to trace where a rule came from.</summary>
    public void RuleAllowed(string tool, string rule) => LogRuleAllowed(logger, tool, rule);

    /// <summary>The edit went back to the model with the rules for its files (agent.md).</summary>
    public void HeldForRules(string tool) => LogApproval(logger, tool, 0, "returned to the model with the rules for its files");

    public void ChatStarted() => LogChatStarted(logger);

    public void ChatOpened(string id) => LogChatOpened(logger, id);

    public void ChatDeleted(string? id) => LogChatDeleted(logger, id ?? "unsaved");

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent turn: chat {ChatId}, question {Length} chars, active file: {Attachment}, attached files: {Files}")]
    private static partial void LogTurnStarted(ILogger logger, string chatId, int length, string attachment, int files);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent turn finished in {ElapsedMs} ms: tool calls {ToolCalls}, approvals {Approvals}")]
    private static partial void LogTurnFinished(ILogger logger, long elapsedMs, int toolCalls, int approvals);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent turn stopped by the user after {ElapsedMs} ms")]
    private static partial void LogTurnStopped(ILogger logger, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent turn failed after {ElapsedMs} ms ({Error})")]
    private static partial void LogTurnFailed(ILogger logger, Exception exception, long elapsedMs, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Edit {Tool} (files: {Files}): {Decision}")]
    private static partial void LogApproval(ILogger logger, string tool, int files, string decision);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool {Tool}: the user allowed '{Rule}' always")]
    private static partial void LogRuleAllowed(ILogger logger, string tool, string rule);

    [LoggerMessage(Level = LogLevel.Debug, Message = "New chat")]
    private static partial void LogChatStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Opened chat {ChatId}")]
    private static partial void LogChatOpened(ILogger logger, string chatId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted chat {ChatId} (to the recycle bin)")]
    private static partial void LogChatDeleted(ILogger logger, string chatId);
}
