namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Where the model's reasoning in the feed comes from: native reasoning is shown as is; a model without it reasons aloud
/// at the prompt's request (ADR 0010).
/// </summary>
public enum ReasoningSource
{
    /// <summary>GPT reasoning summary (Responses API).</summary>
    Summary,

    /// <summary>Claude reasoning via the <c>thinking</c> parameter.</summary>
    Thinking,

    /// <summary>The <c>reasoning_content</c> stream field: DeepSeek, Qwen, Kimi, MiniMax, Gemini, Grok. Some models emit
    /// it only when the request has <c>reasoning_effort</c>, so it is sent by default.</summary>
    Native,

    /// <summary>No native reasoning: the model writes it in <c>&lt;thinking&gt;</c> and the feed shows it as reasoning.</summary>
    ThinkAloud,
}
