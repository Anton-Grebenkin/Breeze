namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Asks a service of the user's what models it has: the check of a new connection and the model list of a saved one.</summary>
public interface IConnectionProbe
{
    /// <param name="key">The API key; <c>null</c> for a service without one.</param>
    /// <exception cref="AgentConfigurationException">The address is not a URL.</exception>
    /// <exception cref="System.ClientModel.ClientResultException">The service returned an error or did not answer.</exception>
    /// <exception cref="System.Text.Json.JsonException">The answer is not a model list.</exception>
    Task<IReadOnlyList<ServiceModel>> ListModelsAsync(AgentConnection connection, string? key, CancellationToken cancellationToken);
}
