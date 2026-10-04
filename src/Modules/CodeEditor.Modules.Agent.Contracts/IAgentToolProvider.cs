using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// A module's contribution to the agent tools. Registered in DI; tools are created on the first chat message.
/// Tool names are <c>snake_case</c>; descriptions and parameters are for the model, in English or Russian.
/// </summary>
public interface IAgentToolProvider
{
    IEnumerable<AITool> CreateTools();
}
