using System.ComponentModel;

namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// Agent mode (<c>agent.mode</c>), as in Copilot, Cursor and Claude Code (ADR 0010): what the agent may do.
/// Reasoning depth is a separate model setting.
/// </summary>
[TypeConverter(typeof(AgentModeConverter))]
public enum AgentMode
{
    /// <summary>All tools: reads, edits, builds, verifies.</summary>
    Agent,

    /// <summary>
    /// Like Agent plus an advisor, a second model at mandatory checkpoints (ADR 0012): risk review before the first
    /// edit, help on stuck signals, a refuting review before the summary; build and tests are mandatory.
    /// </summary>
    Deep,

    /// <summary>Read and search only: answers questions about the code without changing anything.</summary>
    Ask,

    /// <summary>Read, search and ask the user: studies the code and proposes a plan, which Agent mode carries out.</summary>
    Plan,
}
