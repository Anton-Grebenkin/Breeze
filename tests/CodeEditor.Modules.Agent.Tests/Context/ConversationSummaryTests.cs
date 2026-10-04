using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Context;

/// <summary>
/// Compaction summary (ADR 0023): written with the same request the model saw, or by the helper model when there is
/// none or the model didn't answer with a summary. User requests are kept verbatim, including those from earlier
/// compactions and answers to agent questions. The full history goes to a file.
/// </summary>
public sealed class ConversationSummaryTests
{
    private readonly ScriptedChatClient _conversation = new();
    private readonly ScriptedChatClient _helper = new();

    [Fact]
    public async Task ConversationRequestKnown_SummaryByTheSameRequest()
    {
        _conversation.Reply("Резюме разговора.");

        var included = await CompactAsync(new ChatOptions { Instructions = "Промпт" });

        var request = Assert.Single(_conversation.Requests);
        Assert.Equal(History().Count + 1, request.Count);
        Assert.Equal(CompactionPrompts.SummaryRequest, request[^1].Text);
        Assert.Equal("Промпт", _conversation.LastOptions!.Instructions);
        Assert.Empty(_helper.Requests);
        Assert.Equal([CompactedHistory.RequestsHeader, "задача", CompactedHistory.RequestsFooter], included[0].Contents.Select(content => content.ToString()));
        Assert.Equal("[Summary]\nРезюме разговора.\n\nSTATE\n\nTRANSCRIPT", included[1].Text);
        Assert.Equal("c2", included[2].Contents.OfType<FunctionCallContent>().Single().CallId);
    }

    // The first request of a reopened chat has no conversation options yet, so the helper model writes the summary.
    [Fact]
    public async Task ConversationRequestUnknown_HelperWritesTheSummary()
    {
        _helper.Reply("Резюме служебной модели.");

        var included = await CompactAsync(options: null);

        Assert.Empty(_conversation.Requests);
        Assert.Equal(CompactionPrompts.Summary, _helper.Requests[0][0].Text);
        Assert.Contains(included, message => message.Text.StartsWith("[Summary]\nРезюме служебной модели.", StringComparison.Ordinal));
    }

    // A model that calls a tool hasn't written a summary.
    [Fact]
    public async Task ModelCallsATool_HelperWritesTheSummary()
    {
        _conversation.CallTool("c9", "read_file", new Dictionary<string, object?> { ["path"] = "f.cs" });
        _helper.Reply("Резюме служебной модели.");

        await CompactAsync(new ChatOptions());

        Assert.Single(_conversation.Requests);
        Assert.Single(_helper.Requests);
    }

    [Fact]
    public void Requests_FromUserMessages_AnswersAndEarlierSummaries()
    {
        List<ChatMessage> messages =
        [
            CompactedHistory.RequestsMessage(["первый запрос"])!,
            UserRequest("второй запрос"),
            new(ChatRole.User, "служебная заметка без напоминания"),
            new(ChatRole.Assistant, [new FunctionCallContent("q1", WorkflowAgentTools.AskUserName)]),
            new(ChatRole.Tool, [new FunctionResultContent("q1", "да, с тестами")]),
            Call("r1", "a.cs"),
            new(ChatRole.Tool, [new FunctionResultContent("r1", "текст файла")]),
        ];

        Assert.Equal(["первый запрос", "второй запрос", "Answer to the agent's question: да, с тестами"], CompactedHistory.Requests(messages));
    }

    // Requests are taken newest first while they fit ~20K tokens, then listed in order.
    [Fact]
    public void RequestsMessage_KeepsTheNewest_WithinBudget()
    {
        var old = new string('а', CompactedHistory.RequestsBudgetCharacters);

        var texts = CompactedHistory.RequestsMessage([old, "второй", "третий"])!.Contents.Select(content => content.ToString()).ToList();

        Assert.Equal(CompactedHistory.RequestsHeader, texts[0]);
        Assert.EndsWith("…", texts[1], StringComparison.Ordinal);
        Assert.Equal(["второй", "третий", CompactedHistory.RequestsFooter], texts.Skip(2));
        Assert.Null(CompactedHistory.RequestsMessage([]));
    }

    [Fact]
    public void Transcript_HasRequestsCallsAndFullResults_WithoutReasoning()
    {
        var text = HistoryTranscript.Render(History()).ReplaceLineEndings("\n");

        Assert.StartsWith("## User\nзадача\n", text, StringComparison.Ordinal);
        Assert.Contains("## Assistant\n### read_file(path: a.cs) [c1]\n", text, StringComparison.Ordinal);
        Assert.Contains("## Tool results\n### Result [c1]\nтекст a\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("подпись", text, StringComparison.Ordinal);
    }

    // Compacts the history with the summary strategy alone and returns what the model keeps.
    private async Task<List<ChatMessage>> CompactAsync(ChatOptions? options)
    {
        var strategy = new ConversationSummaryStrategy(
            new CompactionModels(_conversation, () => options, _helper), () => "STATE", _ => "TRANSCRIPT", CompactionTriggers.Always, CompactionTriggers.Never);
        return [.. await CompactionProvider.CompactAsync(strategy, History(), NullLogger.Instance, TestContext.Current.CancellationToken)];
    }

    // A user request and five call steps: the request and first step get compacted, the four newest stay.
    private static List<ChatMessage> History() =>
    [
        UserRequest("задача"),
        .. new[] { "a", "b", "c", "d", "e" }.SelectMany((name, index) => new[]
        {
            Call($"c{index + 1}", $"{name}.cs"),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"c{index + 1}", $"текст {name}")]),
        }),
    ];

    // A user message as the editor builds it: text, context and reminder.
    private static ChatMessage UserRequest(string text) =>
        new(ChatRole.User, [new TextContent(text), new TextContent("<context>\nDate: 2026-10-02.\n</context>"), new TextContent("<reminder>Agent mode.</reminder>")]);

    private static ChatMessage Call(string id, string path) => new(ChatRole.Assistant,
    [
        new TextReasoningContent("план") { ProtectedData = "подпись" },
        new FunctionCallContent(id, "read_file", new Dictionary<string, object?> { ["path"] = path }),
    ]);
}
