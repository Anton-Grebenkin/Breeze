namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>
/// "Check the model": a handful of short requests to a model of the user's connection that find out by trial what
/// otherwise takes reading the gateway's documentation. It spends the user's tokens, a few thousand per run.
/// </summary>
public interface IModelProbe
{
    /// <param name="key">The API key; <c>null</c> for a service without one.</param>
    /// <param name="model">The model id the service expects.</param>
    Task<ModelProbeReport> RunAsync(AgentConnection connection, string? key, string model, CancellationToken cancellationToken);
}
