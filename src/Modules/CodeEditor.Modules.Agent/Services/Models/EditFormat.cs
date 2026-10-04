namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>The tool a model edits files with (ADR 0010): each family gets the one it was trained on.</summary>
public enum EditFormat
{
    /// <summary>Exact fragment replacement (<c>apply_edits</c>): Claude, Gemini, DeepSeek and others.</summary>
    Replace,

    /// <summary>V4A patch (<c>apply_patch</c>), as in Codex: GPT-5 and newer.</summary>
    Patch,
}
