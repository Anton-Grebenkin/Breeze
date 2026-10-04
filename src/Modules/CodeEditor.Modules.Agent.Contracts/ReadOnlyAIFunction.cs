using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Contracts;

/// <summary>
/// Marks a tool that only reads the folder: reading, search, navigation, git history (ADR 0012). Such tools are
/// available in every mode and to the scout and critic; the rest only where the agent changes code. Found through
/// wrappers via <c>GetService</c>, like <see cref="ApprovalRequiredAIFunction"/>.
/// </summary>
public sealed class ReadOnlyAIFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
{
    public static bool IsReadOnly(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.GetService<ReadOnlyAIFunction>() is not null;
    }
}
