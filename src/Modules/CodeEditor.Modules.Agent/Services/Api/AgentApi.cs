namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Protocol of the OpenAI-compatible API (<c>agent.api</c>). Newer OpenAI reasoning models call tools only through the
/// Responses API, while other vendors' models in ProxyAPI are available only through Chat Completions.
/// </summary>
public enum AgentApi
{
    /// <summary>By model: <c>openai/…</c> uses the Responses API, the rest use Chat Completions.</summary>
    Auto,

    ChatCompletions,

    Responses,
}
