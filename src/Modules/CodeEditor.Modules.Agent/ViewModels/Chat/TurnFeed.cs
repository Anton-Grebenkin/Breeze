using System.Collections.ObjectModel;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// The feed of one turn: streamed answers, reasoning, tool call rows and token usage. Written only on the UI thread from
/// the turn graph's events (<see cref="AgentTurn"/>). Text before tool calls is progress, not the answer; tagged
/// thinking goes to a reasoning block; an empty answer is removed.
/// </summary>
internal sealed class TurnFeed(ObservableCollection<ChatMessageViewModel> messages, AgentTurnServices services)
{
    private ResponseState? _round;
    private ChatMessageViewModel? _lastAnswer;

    public int ToolCalls { get; private set; }

    /// <summary>The service reported token usage; otherwise the session estimates it from the text.</summary>
    public bool UsageReported { get; private set; }

    /// <summary>
    /// Starts a model round with a placeholder answer at the end of the feed; continuing a cut-off answer reuses it.
    /// After a check (<paramref name="revises"/>) the previous result was intermediate: it becomes progress, so the feed
    /// keeps a single result.
    /// </summary>
    public void StartRound(bool continues, bool revises = false)
    {
        if (revises)
        {
            DemoteLastAnswer();
        }

        var previous = continues && _lastAnswer is not null && messages.Contains(_lastAnswer) ? _lastAnswer : null;
        var answer = previous ?? new ChatMessageViewModel(ChatMessageKind.Assistant);
        answer.IsInProgress = true;
        if (previous is null)
        {
            messages.Add(answer);
        }

        _round = new ResponseState(answer);
        _lastAnswer = answer;
    }

    public void Apply(AIContent content)
    {
        if (_round is not { } response)
        {
            return;
        }

        switch (content)
        {
            case TextContent text:
                Route(response, response.Tags.Push(text.Text));
                break;
            case TextReasoningContent thought when !string.IsNullOrEmpty(thought.Text):
                AppendReasoning(response, thought.Text);
                break;
            case ToolApprovalRequestContent:
                CloseRound(response);
                break;
            case FunctionCallContent call:
                ToolCalls++;
                CloseRound(response);
                var row = new ToolRowViewModel(services.ToolViews.Describe(call));
                response.Tools[call.CallId] = (InsertBefore(response.Answer, new ChatMessageViewModel(ChatMessageKind.Tool, row.Title) { Tool = row, IsInProgress = true }), call);
                break;
            case FunctionResultContent result when response.Tools.TryGetValue(result.CallId, out var finished):
                Finish(finished.Message, services.ToolViews.Describe(finished.Call, result));
                break;
            case UsageContent usage:
                if (response.Usage.Add(usage.Details) is { } previousRequest)
                {
                    RecordUsage(previousRequest);
                }

                break;
        }
    }

    /// <summary>Ends the round (or a stop mid-round): closes the answer and shows helper work as rows.</summary>
    public void FinishRound(bool stopped)
    {
        if (_round is not { } response)
        {
            return;
        }

        _round = null;
        if (stopped)
        {
            response.Answer.Append(response.Answer.Text.Length == 0 ? Strings.Stopped : " …" + Strings.Stopped);
        }

        if (response.Usage.Flush() is { } last)
        {
            RecordUsage(last);
        }

        Route(response, response.Tags.Flush());
        response.Answer.IsInProgress = false;
        EndReasoning(response);
        if (response.Answer.Text.Length == 0)
        {
            messages.Remove(response.Answer);
        }

        ShowHelperWork(response.Answer);
    }

    private void DemoteLastAnswer()
    {
        var index = _lastAnswer is { Text.Length: > 0 } answer ? messages.IndexOf(answer) : -1;
        if (index < 0)
        {
            return;
        }

        messages[index] = new ChatMessageViewModel(ChatMessageKind.Progress, messages[index].Text.Trim());
        _lastAnswer = null;
    }

    /// <summary>Adds a harness row above the answer if it is in the feed, otherwise at the end.</summary>
    public void Notice(ChatMessageKind kind, string text)
    {
        var message = new ChatMessageViewModel(kind, text);
        if (_round?.Answer is { } answer && messages.Contains(answer))
        {
            InsertBefore(answer, message);
        }
        else
        {
            messages.Add(message);
        }
    }

    // Tagged thinking goes to the reasoning block, the rest to the answer (without leading blank lines).
    private void Route(ResponseState response, IReadOnlyList<TaggedText> parts)
    {
        foreach (var part in parts)
        {
            switch (part.Kind)
            {
                case TaggedTextKind.Thinking:
                    AppendReasoning(response, part.Text);
                    break;
                case TaggedTextKind.ThinkingDone:
                    EndReasoning(response);
                    break;
                default:
                    response.Answer.Append(response.Answer.Text.Length == 0 ? part.Text.TrimStart() : part.Text);
                    break;
            }
        }
    }

    private void AppendReasoning(ResponseState response, string text)
    {
        response.Reasoning ??= InsertBefore(response.Answer, new ChatMessageViewModel(ChatMessageKind.Reasoning) { IsInProgress = true });
        response.Reasoning.Append(text);
    }

    private static void EndReasoning(ResponseState response)
    {
        if (response.Reasoning is { } reasoning)
        {
            reasoning.IsInProgress = false;
            response.Reasoning = null;
        }
    }

    // The row title becomes its text in the saved feed: "Read Order.cs", not "Reading Order.cs".
    private static void Finish(ChatMessageViewModel message, Contracts.Feed.AgentToolView view)
    {
        message.Tool!.Update(view);
        message.Text = view.Title;
        message.IsInProgress = false;
    }

    private void RecordUsage(UsageDetails details)
    {
        UsageReported = true;
        services.Usage.Add(details.InputTokenCount, details.OutputTokenCount, details.CachedInputTokenCount);
    }

    /// <summary>
    /// The model moved on to tool calls: text written before them is progress, not the answer. It becomes a separate
    /// row above the calls and the answer starts over, so it holds only the result. The next round's reasoning starts a
    /// new block.
    /// </summary>
    private void CloseRound(ResponseState response)
    {
        Route(response, response.Tags.Flush());
        EndReasoning(response);

        var text = response.Answer.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        InsertBefore(response.Answer, new ChatMessageViewModel(ChatMessageKind.Progress, text));
        response.Answer.Text = string.Empty;
    }

    private ChatMessageViewModel InsertBefore(ChatMessageViewModel anchor, ChatMessageViewModel message)
    {
        messages.Insert(messages.IndexOf(anchor), message);
        return message;
    }

    // Helper model work in this round: usage goes to chat spend, compaction and advice become rows above the answer.
    private void ShowHelperWork(ChatMessageViewModel answer)
    {
        var helpers = services.HelperUsage.Take();
        if (helpers.InputTokens + helpers.OutputTokens > 0)
        {
            services.Usage.AddAuxiliary(helpers.InputTokens, helpers.OutputTokens, helpers.CachedInputTokens);
        }

        foreach (var notice in services.Compaction.TakeNotices())
        {
            Above(answer, new ChatMessageViewModel(ChatMessageKind.Status, notice));
        }

        foreach (var notice in services.Deep.TakeNotices())
        {
            Above(answer, new ChatMessageViewModel(ChatMessageKind.Progress, notice));
        }
    }

    private void Above(ChatMessageViewModel answer, ChatMessageViewModel message)
    {
        if (messages.Contains(answer))
        {
            InsertBefore(answer, message);
        }
        else
        {
            messages.Add(message);
        }
    }
}
