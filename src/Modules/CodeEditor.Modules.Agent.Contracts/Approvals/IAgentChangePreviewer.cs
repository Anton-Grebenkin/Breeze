namespace CodeEditor.Modules.Agent.Contracts.Approvals;

/// <summary>
/// Previews the changes of a tool that needs approval, so the chat shows the diff before the tool runs. Previewing
/// changes nothing.
/// </summary>
public interface IAgentChangePreviewer
{
    bool CanPreview(string toolName);

    /// <exception cref="AgentToolException">The edit cannot be applied (text not found, file already exists).</exception>
    Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken);
}
