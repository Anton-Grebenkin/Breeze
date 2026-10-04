using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Context;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// The user message to the agent: text, a <c>&lt;context&gt;</c> block (date and module contributions such as the open
/// file and selected lines) and a <c>&lt;reminder&gt;</c> (as in Copilot: it works better next to the request) with the
/// current mode and the model family adjustment. Volatile data lives here in the last message, not in the system
/// prompt, so the earlier conversation stays unchanged and cacheable by the provider.
/// </summary>
public sealed partial class TurnMessageBuilder(
    IEnumerable<IAgentContextProvider> providers,
    ChatStartContext start,
    AttachmentReader attachments,
    IOptionsMonitor<AgentOptions> options,
    TimeProvider time,
    ILogger<TurnMessageBuilder> logger)
{
    /// <param name="startsChat">The chat's first message: it carries the folder snapshot and memory note list (ADR 0012).</param>
    /// <param name="previousMode">Mode of this chat's previous request; <c>null</c> if none or unknown.</param>
    /// <param name="files">Attached files, placed right after the request text (<see cref="AttachmentReader"/>).</param>
    public async Task<ChatMessage> BuildAsync(string text, bool includeActiveEditor, bool startsChat, AgentMode? previousMode, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var lines = new List<string> { string.Create(CultureInfo.InvariantCulture, $"Date: {time.GetLocalNow():yyyy-MM-dd}.") };
        var request = new AgentContextRequest(includeActiveEditor, text);
        foreach (var provider in providers)
        {
            lines.AddRange(await CollectAsync(provider, request, cancellationToken));
        }

        List<AIContent> contents = [new TextContent(text), .. await attachments.ReadAsync(files, cancellationToken)];
        if (startsChat)
        {
            contents.AddRange((await start.BuildAsync(cancellationToken)).Select(block => new TextContent(block)));
        }

        contents.Add(new TextContent($"<context>\n{string.Join('\n', lines)}\n</context>"));
        contents.Add(new TextContent($"<reminder>{Reminder(options.CurrentValue, text, previousMode)}</reminder>"));
        return new ChatMessage(ChatRole.User, contents);
    }

    // Mode change, mode, item check for multi-requirement requests, family reminder, request language and, for
    // think-aloud models, the reasoning tags.
    private static string Reminder(AgentOptions options, string text, AgentMode? previousMode)
    {
        var thinksAloud = ModelRequest.ThinksAloud(options);
        string[] parts =
        [
            previousMode is { } previous && previous != options.Mode ? PromptSections.ModeChange(previous, options.Mode) : string.Empty,
            PromptSections.ModeReminder(options.Mode),
            AgentModes.CanChange(options.Mode) && TaskRequirements.AreSeveral(text) ? PromptSections.RequirementsReminder : string.Empty,
            ModelProfiles.For(options.Model).Reminder,
            RequestLanguage.Reminder(text, thinksAloud),
            thinksAloud ? PromptSections.ThinkAloudReminder : string.Empty,
        ];
        return string.Join(' ', parts.Where(static part => part.Length > 0));
    }

    // One failing module must not cost the user an answer: its lines are simply left out.
    private async Task<IReadOnlyList<string>> CollectAsync(IAgentContextProvider provider, AgentContextRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetContextAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProviderFailed(logger, exception, provider.GetType().Name);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent context from {Provider} was not received")]
    private static partial void LogProviderFailed(ILogger logger, Exception exception, string provider);
}
