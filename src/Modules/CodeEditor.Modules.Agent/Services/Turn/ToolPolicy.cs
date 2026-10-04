using CodeEditor.Modules.Agent.Contracts;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Which tools a mode allows (ADR 0012). The model sees the same tool list in every mode, so the request prefix stays
/// cacheable; the restriction applies at call time. Ask only reads, Plan can also ask the user; edits, commands,
/// builds and tests run only in modes that change code. Folder memory and page reading work in every mode: they do not
/// change code (an unfamiliar site still needs a card).
/// </summary>
public static class ToolPolicy
{
    public static bool Allows(AgentMode mode, AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return AgentModes.CanChange(mode)
            || ReadOnlyAIFunction.IsReadOnly(tool)
            || tool.Name is MemoryAgentTools.MemoryName or WebAgentTools.FetchName
            || (mode == AgentMode.Plan && tool.Name == WorkflowAgentTools.AskUserName);
    }
}
