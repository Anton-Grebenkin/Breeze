namespace CodeEditor.Modules.Agent.ViewModels.Chat;

public enum ChatMessageKind
{
    User,

    /// <summary>Model answer in Markdown.</summary>
    Assistant,

    /// <summary>Agent tool call: name, arguments, status.</summary>
    Tool,

    /// <summary>Approval card for agent edits.</summary>
    Approval,

    /// <summary>Request error with a hint on what to do.</summary>
    Error,

    /// <summary>Model reasoning before the answer, collapsed; only when the model sends it.</summary>
    Reasoning,

    /// <summary>Agent question to the user (<c>ask_user</c>) with answer options.</summary>
    Question,

    /// <summary>Harness notice: the turn hit the request limit; has a Continue button.</summary>
    Notice,

    /// <summary>Harness status line: a check after edits, or edits left unchecked.</summary>
    Status,

    /// <summary>Reviewer objections (chats before ADR 0010 only): the verdict line, then a list.</summary>
    Review,

    /// <summary>Model text between tool calls: work progress rather than the answer.</summary>
    Progress,

    /// <summary>Plan is ready (Plan mode); "Execute plan" switches to Agent mode.</summary>
    Handoff,
}
