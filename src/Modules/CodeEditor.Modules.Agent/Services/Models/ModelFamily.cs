namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>Model family: each has its own quirks that the prompt compensates for (<see cref="ModelProfile"/>).</summary>
public enum ModelFamily
{
    Claude,

    /// <summary>GPT‑4 and earlier: tend to stop mid-work.</summary>
    Gpt,

    /// <summary>GPT‑5 and the o-series: reason on their own, favor long plans.</summary>
    GptReasoning,

    /// <summary>Gemini: sometimes describes a tool call in text instead of making it.</summary>
    Gemini,

    DeepSeek,

    /// <summary>
    /// Strong models of other vendors (Grok, Qwen 3, Kimi, GLM, MiniMax, MiMo): call tools in batches unless asked to go
    /// step by step (with a step-by-step rule Grok made only 1–2 calls per request).
    /// </summary>
    Frontier,

    /// <summary>Others, including local ones: short imperative rules, one step at a time.</summary>
    Other,
}
