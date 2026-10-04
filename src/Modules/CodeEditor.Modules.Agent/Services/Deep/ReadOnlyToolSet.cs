using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// Read tools for the explorer and critic subagents (ADR 0012): read-only tools as is, and approval-required tools
/// (commands) limited to their read-only calls, since subagents have no cards. The agent updates the set when it
/// builds its tools.
/// </summary>
public sealed class ReadOnlyToolSet(IEnumerable<IAgentApprovalPolicy> policies)
{
    /// <summary>
    /// Tools subagents do not need: subagents themselves, user questions and the web (their job is the folder's code);
    /// an image has nowhere to go, the main turn's checks node delivers it.
    /// </summary>
    private static readonly string[] Excluded =
        [ExploreAgent.ToolName, WorkflowAgentTools.AskUserName, WebAgentTools.SearchName, WebAgentTools.FetchName, ImageAgentTools.ViewImageName];

    private IReadOnlyList<AIFunction> _tools = [];

    public IReadOnlyList<AIFunction> Tools => Volatile.Read(ref _tools);

    public void Update(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var functions = tools.OfType<AIFunction>().Where(tool => !Excluded.Contains(tool.Name)).ToList();
        IReadOnlyList<AIFunction> readTools =
        [
            .. functions.Where(ReadOnlyAIFunction.IsReadOnly),
            .. functions
                .Where(tool => !ReadOnlyAIFunction.IsReadOnly(tool))
                .Select(tool => (Tool: tool, Policy: policies.FirstOrDefault(policy => policy.CanDecide(tool.Name))))
                .Where(pair => pair.Policy is not null)
                .Select(pair => (AIFunction)new ReadOnlyAIFunction(new ReadOnlyCallsFunction(pair.Tool, pair.Policy!))),
        ];
        Volatile.Write(ref _tools, readTools);
    }
}
