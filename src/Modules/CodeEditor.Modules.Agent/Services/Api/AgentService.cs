namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// A model service with an OpenAI-compatible API: endpoint, its key name in the secret store, the models offered and
/// the dialect (which non-standard request fields it accepts).
/// </summary>
public sealed record AgentService(string Title, string Endpoint, string SecretName, IReadOnlyList<string> Models, ServiceDialect Dialect)
{
    /// <summary>The service's site, where the user gets a key; shown in the key dialog.</summary>
    public Uri? KeyPage { get; init; }
}
