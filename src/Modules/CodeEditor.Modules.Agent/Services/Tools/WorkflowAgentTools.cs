using System.ComponentModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// The agent's own workflow tools: a question to the user (<c>ask_user</c>), a diff of its edits (<c>get_changes</c>)
/// and the task plan (<c>manage_todo</c>). The mode decides which are available (<see cref="ToolPolicy"/>).
/// </summary>
public sealed class WorkflowAgentTools(TodoList todos, UserQuestions questions, ChatChanges changes, IAgentOutputStore outputs) : IAgentToolProvider
{
    public const string ManageTodoName = "manage_todo";
    public const string AskUserName = "ask_user";
    public const string GetChangesName = "get_changes";
    public const int MaxOptions = 5;

    public IEnumerable<AITool> CreateTools()
    {
        yield return AIFunctionFactory.Create(AskUserAsync, AskUserName,
            "Asks the user one question and waits for the answer. Use only when the answer changes what you do next and cannot be found in the code or chat. " +
            "Offer 2–5 short options when possible (the user may still answer freely). Never ask for passwords, keys or tokens; do not ask to approve a plan — just proceed.");
        yield return new ReadOnlyAIFunction(AIFunctionFactory.Create(GetChangesAsync, GetChangesName,
            "Returns a unified diff of all file changes you made in this chat (current text, including unsaved edits, against the text before your first edit). " +
            "Use it to review your work against the task before the final answer."));
        yield return AIFunctionFactory.Create(ManageTodo, ManageTodoName,
            "Replaces the task plan shown to the user with the full list you pass. Use for work of 3+ steps: create the plan before starting, " +
            "keep exactly one item 'in_progress', mark each item 'completed' as soon as it is done (not in batches). " +
            "When the request lists several requirements, each requirement is an item, with how it will be verified. " +
            "Never mark an item completed while its build or tests fail. Skip it for simple one-step tasks.");
    }

    private async Task<string> AskUserAsync(
        [Description("The question, one or two sentences.")] string question,
        [Description("Optional short answer options, up to 5.")] string[]? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new AgentToolException(Strings.QuestionEmpty);
        }

        var choices = (options ?? []).Where(option => !string.IsNullOrWhiteSpace(option)).Select(option => option.Trim()).Take(MaxOptions).ToList();
        var answer = await questions.AskAsync(question.Trim(), choices, cancellationToken);
        return string.Format(CultureInfo.CurrentCulture, Strings.UserAnswered, answer);
    }

    private async Task<string> GetChangesAsync()
    {
        var diff = ChatChanges.Diff(await changes.CollectAsync());
        return diff.Length == 0 ? Strings.NoChangesInChat : outputs.Fit(diff, GetChangesName);
    }

    private string ManageTodo([Description("The whole plan in order; each call replaces the previous one.")] TodoItemInput[] items)
    {
        if (items.Length == 0)
        {
            todos.Clear();
            return Strings.PlanCleared;
        }

        var parsed = items.Select(Parse).ToList();
        if (parsed.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != parsed.Count)
        {
            throw new AgentToolException(Strings.PlanDuplicateIds);
        }

        if (parsed.Count(item => item.Status == TodoStatus.InProgress) > 1)
        {
            throw new AgentToolException(Strings.PlanSingleInProgress);
        }

        todos.Replace(parsed);
        return Strings.PlanUpdated + "\n" + todos.Render() + Warnings(parsed);
    }

    private static TodoItem Parse(TodoItemInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.PlanItemEmptyTitle, input.Id));
        }

        var status = input.Status?.Trim().ToLowerInvariant() switch
        {
            "pending" => TodoStatus.Pending,
            "in_progress" or "in-progress" or "inprogress" => TodoStatus.InProgress,
            "completed" or "done" => TodoStatus.Completed,
            _ => throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.PlanItemUnknownStatus, input.Id, input.Status)),
        };
        return new TodoItem(string.IsNullOrWhiteSpace(input.Id) ? input.Title : input.Id.Trim(), input.Title.Trim(), status);
    }

    private static string Warnings(List<TodoItem> items) => items.Count switch
    {
        < TodoList.SuggestedMinimum => "\n" + Strings.PlanTooShort,
        > TodoList.SuggestedMaximum => "\n" + Strings.PlanTooLong,
        _ => string.Empty,
    };
}
