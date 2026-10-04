using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Creates the model client from settings; tests substitute a fake model.</summary>
public interface IChatClientFactory
{
    /// <exception cref="AgentConfigurationException">No API key, or the endpoint is not a URL.</exception>
    IChatClient Create(AgentOptions options);
}
