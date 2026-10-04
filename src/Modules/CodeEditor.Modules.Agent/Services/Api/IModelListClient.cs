namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Lists the models of the configured service (<c>GET /models</c> of an OpenAI-compatible API).</summary>
public interface IModelListClient
{
    /// <exception cref="AgentConfigurationException">No API key, or the endpoint is not a URL.</exception>
    /// <exception cref="System.ClientModel.ClientResultException">The service returned an error.</exception>
    Task<IReadOnlyList<string>> ListAsync(AgentOptions options, CancellationToken cancellationToken);
}
