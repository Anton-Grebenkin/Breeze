namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Where a model reference from settings leads (ADR 0055): the service address, the key's name, what the service
/// accepts, and the model as that service knows it. Built by <see cref="ModelRoutes"/>.
/// </summary>
/// <param name="Model">The model id as written for the service, without the connection.</param>
/// <param name="SecretName">Name of the API key in the secret store.</param>
public sealed record ModelRoute(string Model, string Endpoint, string SecretName, ServiceDialect Dialect)
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

    /// <summary>One of the built-in services; <c>null</c> for a connection or an address of the user's own.</summary>
    public AgentService? Service { get; init; }

    /// <summary>The user's connection the model belongs to; <c>null</c> for the service at <c>agent.endpoint</c>.</summary>
    public AgentConnection? Connection { get; init; }

    /// <summary>What the user said about the model; <c>null</c> when it is guessed from the id.</summary>
    public ModelDescription? Description { get; init; }

    public AgentApi Api { get; init; }

    /// <summary>The model's name for the user.</summary>
    public string Title => Description?.Name is { Length: > 0 } name ? name : Model;

    /// <summary>The service's name for the user: the connection, the built-in service, or the host of an own address.</summary>
    public string ServiceTitle =>
        Connection is { } connection ? (connection.Name is { Length: > 0 } name ? name : connection.Id)
        : Service?.Title ?? (Uri.TryCreate(Endpoint, UriKind.Absolute, out var address) ? address.Host : Endpoint);

    public bool Keyless => Connection?.Keyless == true;

    public IReadOnlyDictionary<string, string> Headers => Connection?.Headers ?? NoHeaders;

    /// <summary>The model is one of the vendor's (<c>anthropic/…</c>), as the built-in services name them.</summary>
    public bool IsVendor(string prefix) => Model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
