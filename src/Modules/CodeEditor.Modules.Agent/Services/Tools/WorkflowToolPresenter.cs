using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Feed lines for the agent's own tools (ADR 0010): "Plan · Check the reserve (2 of 5)", "Question to the user",
/// "Reviewing own changes · 3 files".
/// </summary>
public sealed class WorkflowToolPresenter : IAgentToolPresenter
{
    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            WorkflowAgentTools.ManageTodoName => Plan(call),
            WorkflowAgentTools.AskUserName => new AgentToolView(AgentToolIcon.Question, Strings.ToolAskUser),
            WorkflowAgentTools.GetChangesName => new AgentToolView(AgentToolIcon.Diff, Strings.ToolReviewChanges)
            {
                IsExploration = true,
                Detail = call.Result is { } diff ? Plural.Format(ChangedFiles(diff), Strings.FileForms) : null,
            },
            MemoryAgentTools.MemoryName => Memory(call),
            ExploreAgent.ToolName => new AgentToolView(AgentToolIcon.Explore, string.Format(CultureInfo.CurrentCulture, Strings.ToolExplore, Shorten(call.Text("question") ?? "?")))
            {
                IsExploration = true,
            },
            _ => null,
        };
    }

    private const int MaxQuestionLength = 70;

    private static string Shorten(string text) => text.Length <= MaxQuestionLength ? text : string.Concat(text.AsSpan(0, MaxQuestionLength), "…");

    // "Saved: build-commands", "Memory: build-commands", "Forgotten: old-note".
    private static AgentToolView Memory(AgentToolCall call)
    {
        var format = call.Text("action")?.Trim().ToLowerInvariant() switch
        {
            "save" => Strings.ToolMemorySave,
            "delete" => Strings.ToolMemoryDelete,
            _ => Strings.ToolMemoryRead,
        };
        return new AgentToolView(AgentToolIcon.Memory, string.Format(CultureInfo.CurrentCulture, format, call.Text("name") ?? "?"))
        {
            IsExploration = format == Strings.ToolMemoryRead,
        };
    }

    private static AgentToolView Plan(AgentToolCall call)
    {
        var items = call.Items("items");
        var current = items.FirstOrDefault(item => Status(item) is "in_progress" or "in-progress" or "inprogress");
        var done = items.Count(item => Status(item) is "completed" or "done");
        var title = current.ValueKind == JsonValueKind.Object && current.TryGetProperty("title", out var name)
            ? string.Format(CultureInfo.CurrentCulture, Strings.ToolPlanItem, name.GetString())
            : Strings.ToolPlanUpdated;
        var detail = items.Count == 0 ? Strings.ToolPlanCleared : string.Format(CultureInfo.CurrentCulture, Strings.ProgressOf, done, items.Count);
        return new AgentToolView(AgentToolIcon.Plan, title) { Detail = detail };
    }

    private static string? Status(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("status", out var status) ? status.GetString()?.Trim().ToLowerInvariant() : null;

    // Unified diff: each file has a line starting with "+++ ". Counted without splitting the diff into lines.
    private static int ChangedFiles(string diff) =>
        (diff.StartsWith("+++ ", StringComparison.Ordinal) ? 1 : 0) + diff.AsSpan().Count("\n+++ ");
}
