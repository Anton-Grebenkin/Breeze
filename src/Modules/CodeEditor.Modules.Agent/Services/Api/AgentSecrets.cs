namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Names of the agent's secrets in <c>ISecretStore</c>.</summary>
public static class AgentSecrets
{
    /// <summary>Key for ProxyAPI and for any endpoint outside <see cref="AgentServices"/>.</summary>
    public const string ApiKey = "agent-api-key";

    public const string ProvodApiKey = "agent-api-key-provod";

    public const string AitunnelApiKey = "agent-api-key-aitunnel";
}
