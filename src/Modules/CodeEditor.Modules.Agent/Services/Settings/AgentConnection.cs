namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// The user's own connection to a model service (<c>agent.connections</c>, ADR 0055): a gateway at work, a vendor's API,
/// a local server. Its key lives in the secret store under the connection id, not here. A model of the connection is
/// referred to as <c>id:model</c> (<see cref="ModelRoutes"/>). Read only from the user's settings: a folder cannot send
/// the conversation to an address of its own.
/// </summary>
public sealed class AgentConnection
{
    /// <summary>Short name used in model references and for the key: letters, digits, <c>-</c> and <c>_</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name shown in the editor; the id when empty.</summary>
    public string? Name { get; set; }

    /// <summary>Address of the OpenAI-compatible API, usually ending in <c>/v1</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Protocol; by model when automatic, as for the built-in services.</summary>
    public AgentApi Api { get; set; } = AgentApi.Auto;

    /// <summary>Headers added to every request: a team, a project, a route of the gateway.</summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The service asks for no key (Ollama, LM Studio).</summary>
    public bool Keyless { get; set; }

    /// <summary>The service rejects <c>max_completion_tokens</c>: the response limit goes as <c>max_tokens</c>.</summary>
    public bool LegacyMaxTokens { get; set; }

    /// <summary>Models of the connection the user described; any other id of the service works too, guessed by name.</summary>
    public List<ModelDescription> Models { get; set; } = [];
}
