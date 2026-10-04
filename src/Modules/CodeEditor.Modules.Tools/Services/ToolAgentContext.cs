using CodeEditor.Modules.Agent.Contracts.Context;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>The shelf in the <c>&lt;context&gt;</c> of every message: the model picks a tool by its description.</summary>
public sealed class ToolAgentContext(ToolShelf shelf) : IAgentContextProvider
{
    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        var tools = shelf.Tools;
        if (tools.IsEmpty)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        var lines = tools.Select(tool => $"- {tool.Name}: {tool.Description.TrimEnd('.')}." + (tool.Parameters.Count == 0 ? string.Empty
            : " Parameters: " + string.Join("; ", tool.Parameters.Select(parameter => parameter.Description.Length > 0 ? $"{parameter.Name} — {parameter.Description}" : parameter.Name)) + "."));
        return ValueTask.FromResult<IReadOnlyList<string>>([$"Tool shelf (call with {ToolAgentTools.RunToolName}):\n" + string.Join('\n', lines)]);
    }
}
