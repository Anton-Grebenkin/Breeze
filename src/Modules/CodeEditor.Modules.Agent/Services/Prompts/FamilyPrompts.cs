namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// Prompt adjustments per model family, like Copilot's prompt registry and Cursor's per-model prompts (ADR 0010): a
/// system prompt section plus a short reminder in the last message, which works better next to the request and leaves
/// the system prompt unchanged.
/// </summary>
public static class FamilyPrompts
{
    /// <summary>Claude: act rather than advise; don't over-explore; it writes interim notes on its own.</summary>
    public const string Claude = """
        - When the user asks for a change, make it rather than suggesting it; find missing details with tools.
        - Call independent tools in parallel in one response; call dependent ones in sequence.
        - Stop exploring as soon as you have enough context to act.
        - After two failed attempts with one approach, switch approaches.
        """;

    public const string ClaudeReminder = "Keep going until the task is done, stay within its scope, and write notes and answer in the language of the user's request.";

    /// <summary>
    /// GPT-5+: persistence and a concrete plan. The reasoning summary is visible to the user, so preambles before calls
    /// (OpenAI's advice for UIs without reasoning) are not needed: they would just repeat the summary.
    /// </summary>
    public const string GptReasoning = """
        - You are an agent: keep working until the request is fully resolved before yielding to the user. When unsure, research or choose the most reasonable option and continue rather than stopping.
        - Your reasoning summary is shown to the user, so don't restate your intent as text before tool calls.
        - Batch independent reads and searches into one response — they run in parallel; don't read related files one per response.
        - Keep plans short and concrete: a step names the file or symbol and the change ("Reserve: check totals per product before subtracting"), never "explore the code" or "implement the feature".
        - After the first edit run the narrowest check (build, a filtered test) before widening the work.
        """;

    public const string GptReasoningReminder = "Keep working until the request is fully resolved. Write notes and answer in the language of the user's request.";

    /// <summary>GPT‑4 and earlier: tend to stop mid-work and promise a call instead of making it.</summary>
    public const string Gpt = """
        - You are an agent: keep working until the request is fully resolved, then yield to the user.
        - If you say you will call a tool, call it in the same response.
        """;

    public const string GptReminder = "Keep working until the request is fully resolved; call tools instead of describing them. Write notes and answer in the language of the user's request.";

    /// <summary>Gemini: sometimes describes a call in text instead of calling the function.</summary>
    public const string Gemini = """
        - Call tools only through function calling — never write a tool call as text.
        - Keep working until the task is fully resolved.
        """;

    public const string GeminiReminder = "Call tools through function calling, not text. Keep going until the task is done. Write notes and answer in the language of the user's request.";

    /// <summary>
    /// DeepSeek, Grok, Qwen 3, Kimi, GLM, MiniMax: capable of parallel calls, but without a reminder they go one at a
    /// time and rephrase searches instead of acting on what they found.
    /// </summary>
    public const string Frontier = """
        - Batch independent calls in one response: several searches at once, then several whole files at once; call dependent ones in sequence.
        - Call tools through function calling, never describe a call in text.
        - Once you know where the problem is, act on it: make the change and verify it instead of searching again.
        - After two failed attempts with one approach, switch approaches.
        """;

    public const string FrontierReminder = "Batch independent tool calls in one response; keep going until the task is done. Write notes and answer in the language of the user's request.";

    /// <summary>Others, including local ones: short imperative rules, one step at a time.</summary>
    public const string Direct = """
        - Work step by step: call a tool, read its result, decide the next step.
        - Call tools through function calling, never describe a call in text.
        - Follow the rules literally and keep answers concise.
        """;

    public const string DirectReminder = "One step at a time through function calls; keep going until the task is done. Write notes and answer in the language of the user's request.";

    public static (string Guidance, string Reminder) For(ModelFamily family) => family switch
    {
        ModelFamily.Claude => (Claude, ClaudeReminder),
        ModelFamily.GptReasoning => (GptReasoning, GptReasoningReminder),
        ModelFamily.Gpt => (Gpt, GptReminder),
        ModelFamily.Gemini => (Gemini, GeminiReminder),
        ModelFamily.DeepSeek or ModelFamily.Frontier => (Frontier, FrontierReminder),
        _ => (Direct, DirectReminder),
    };
}
