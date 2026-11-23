using CodeEditor.Modules.Agent.Contracts;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Which tools a mode allows (ADR 0012). The model sees the same tool list in every mode, so the request prefix stays
/// cacheable; the restriction applies at call time. Ask only reads, Plan can also ask the user; edits, commands,
/// builds and tests run only in modes that change code. Folder memory and page reading work in every mode: they do not
/// change code (an unfamiliar site still needs a card). A tool of another module that changes something only in some
/// calls (the browser) is judged per call by its module: <see cref="ApprovalCards.ReadsInEveryMode"/>.
/// </summary>
public static class ToolPolicy
{
    public static bool Allows(AgentMode mode, AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return AllowsByName(mode, tool.Name) || ReadOnlyAIFunction.IsReadOnly(tool);
    }

    /// <summary>The same for a call known only by its name (an approval request), where the read-only mark is not seen.</summary>
    public static bool AllowsByName(AgentMode mode, string toolName) =>
        AgentModes.CanChange(mode)
        || toolName is MemoryAgentTools.MemoryName or WebAgentTools.FetchName
        || (mode == AgentMode.Plan && toolName == WorkflowAgentTools.AskUserName);
}
