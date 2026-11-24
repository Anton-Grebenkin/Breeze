namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Resolves a model reference (ADR 0055). <c>work:team-coder</c> is the model <c>team-coder</c> of the user's
/// connection <c>work</c>; a reference without a known connection in front is a model of the service at
/// <c>agent.endpoint</c>, as before connections. Model ids may contain a colon themselves (<c>qwen3:8b</c>), so only
/// the id of an existing connection counts as one.
/// </summary>
public static class ModelRoutes
{
    public const char Separator = ':';

    /// <summary>The route of the conversation model.</summary>
    public static ModelRoute For(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return For(options, options.Model);
    }

    /// <summary>The route of another model of the same settings: a helper, the scout, the search.</summary>
    public static ModelRoute For(AgentOptions options, string reference)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reference);
        if (ConnectionOf(options, reference) is { } connection)
        {
            var model = reference[(connection.Id.Length + 1)..];
            var dialect = connection.LegacyMaxTokens ? ServiceDialect.Standard with { LegacyMaxTokens = true } : ServiceDialect.Standard;
            return new ModelRoute(model, connection.Endpoint, AgentSecrets.ForConnection(connection.Id), dialect)
            {
                Connection = connection,
                Description = connection.Models.FirstOrDefault(described => string.Equals(described.Id, model, StringComparison.OrdinalIgnoreCase)),
                Api = connection.Api,
            };
        }

        var service = AgentServices.For(options.Endpoint);
        return new ModelRoute(reference, options.Endpoint, service?.SecretName ?? AgentSecrets.ApiKey, service?.Dialect ?? ServiceDialect.Standard)
        {
            Service = service,
            Api = options.Api,
        };
    }

    /// <summary>The reference to a model of a connection.</summary>
    public static string Reference(AgentConnection connection, string model)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.Id + Separator + model;
    }

    private static AgentConnection? ConnectionOf(AgentOptions options, string reference)
    {
        var end = reference.IndexOf(Separator, StringComparison.Ordinal);
        return end <= 0 ? null : options.Connections.FirstOrDefault(connection => connection.Id.Length == end && reference.StartsWith(connection.Id, StringComparison.OrdinalIgnoreCase));
    }
}
