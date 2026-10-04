using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Rules;

namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// The agent system prompt, built from sections in a stable order (ADR 0010): role and folder → how to work → tools →
/// edits → verification and summary → style → modes → model specifics → project rules. Built when the agent is
/// created; a folder switch raises <see cref="Changed"/> and rule file edits set <see cref="TakeRulesChanged"/>, and the
/// agent is recreated. Volatile data (open file, date, current mode) goes into the user message instead, so the prefix
/// stays cacheable (ADR 0012).
/// </summary>
public sealed class SystemPrompt : IDisposable
{
    private readonly IWorkspace _workspace;
    private readonly ProjectInstructions _instructions;
    private int _rulesChanged;

    public SystemPrompt(IWorkspace workspace, ProjectInstructions instructions)
    {
        _workspace = workspace;
        _instructions = instructions;
        _workspace.Changed += OnWorkspaceChanged;
        _workspace.FilesChanged += OnFilesChanged;
    }

    /// <summary>The instructions are stale: another folder was opened.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Rule files changed since the last call. Checked before a request, not raised as <see cref="Changed"/>: the agent
    /// may edit AGENTS.md in the middle of a turn, and its client must not be disposed under a running request.
    /// </summary>
    public bool TakeRulesChanged() => Interlocked.Exchange(ref _rulesChanged, 0) == 1;

    public string Build(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var profile = ModelProfiles.For(options.Model);
        string?[] sections =
        [
            PromptSections.Role(_workspace.Name, _workspace.Root),
            PromptSections.Principles,
            PromptSections.Tools,
            PromptSections.Diagnosis,
            PromptSections.Editing(profile.EditFormat),
            PromptSections.Verification,
            PromptSections.Style,
            ModelRequest.ThinksAloud(options) ? PromptSections.ThinkAloud : null,
            _workspace.Root is null ? null : PromptSections.Memory,
            PromptSections.Modes,
            PromptSections.Model(profile),
            RulesPrompt.Build(_instructions.Load()),
        ];

        return string.Join("\n\n", sections.Where(section => !string.IsNullOrWhiteSpace(section)).Select(section => section!.Trim()));
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _workspace.FilesChanged -= OnFilesChanged;
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        if (e.Changes.Any(change => ProjectInstructions.IsRulesFile(_workspace.RelativePath(change.Path))))
        {
            Interlocked.Exchange(ref _rulesChanged, 1);
        }
    }
}
