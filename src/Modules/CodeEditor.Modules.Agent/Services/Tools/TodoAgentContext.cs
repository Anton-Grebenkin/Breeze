using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>Puts an unfinished plan into every message's <c>&lt;context&gt;</c> block, so the model continues where it was.</summary>
public sealed class TodoAgentContext(TodoList todos) : IAgentContextProvider
{
    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<string>>(todos.HasOpenItems ? [Strings.CurrentPlan + "\n" + todos.Render()] : []);
}
