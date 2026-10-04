using System.ComponentModel;
using System.Globalization;
using System.Text;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Agent edits, applied after approval (or at once in auto mode). Edits open the files in tabs, go into their buffers
/// as one undo step per file and are saved right away, as in Cursor: builds and tests see them, and the changes stay
/// highlighted in the editor until the user accepts or rejects them (ADR 0040).
/// Two formats for model families (ADR 0010): exact replacement (<c>apply_edits</c>) and V4A patch (<c>apply_patch</c>,
/// GPT-5 and newer); a patch is parsed into the same edits.
/// </summary>
public sealed class EditingAgentTools(
    EditPlanner planner,
    EditorAreaViewModel editors,
    IWorkspace workspace,
    IFileSystem fileSystem,
    IUiDispatcher dispatcher,
    IAgentFileState fileState,
    IDocumentService documents) : IAgentToolProvider, IAgentChangePreviewer
{
    public const string ApplyEditsName = "apply_edits";
    public const string ApplyPatchName = "apply_patch";
    public const string CreateFileName = "create_file";

    private const string PatchDescription =
        "Edits files with a patch in the apply_patch (V4A) format. Wrap it in '*** Begin Patch' and '*** End Patch'. " +
        "For each file write '*** Update File: <path>' followed by hunks: an optional '@@ <line>' naming a nearby unique line above the change " +
        "(a class or method signature), then lines prefixed with ' ' (context, about 3 lines before and after), '-' (removed) or '+' (added). " +
        "A new file is '*** Add File: <path>' with every line prefixed by '+'. Paths are relative to the workspace root. " +
        "Read the file first; context must match it exactly. To delete or move files use delete_file or move_file. " +
        "The whole patch is shown to the user as a diff and applied after approval.";

    public IEnumerable<AITool> CreateTools() =>
    [
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ApplyEditsAsync, ApplyEditsName,
            "Edits workspace files by exact text replacement. Each edit replaces oldText (must occur exactly once in its file; copy it from read_file without line numbers) with newText. All edits of one call are shown to the user as a diff and applied together after approval.")),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ApplyPatchAsync, ApplyPatchName, PatchDescription)),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(CreateFileAsync, CreateFileName,
            "Creates a new text file in the workspace with the given content. Fails if the file exists. Requires user approval.")),
    ];

    public bool CanPreview(string toolName) => toolName is ApplyEditsName or ApplyPatchName or CreateFileName;

    public async Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken) => toolName switch
    {
        CreateFileName => [CreatePreview(ToolArguments.Get<string>(arguments, "path"), ToolArguments.Get<string>(arguments, "content"))],
        ApplyPatchName => await PatchPreviewAsync(PatchParser.Parse(ToolArguments.Get<string>(arguments, "input"))),
        _ => EditPreviews(await planner.PlanAsync(WithPath(ToolArguments.Get<FileEdit[]>(arguments, "edits"), ToolArguments.GetOptional<string>(arguments, "path")))),
    };

    /// <summary>
    /// A shared path for edits without their own, as in Claude Code's MultiEdit: many models send
    /// <c>{"path": "...", "edits": [{"oldText", "newText"}]}</c>.
    /// </summary>
    private static FileEdit[] WithPath(FileEdit[] edits, string? path) =>
        string.IsNullOrWhiteSpace(path) ? edits : [.. edits.Select(edit => string.IsNullOrWhiteSpace(edit.Path) ? edit with { Path = path } : edit)];

    private async Task<IReadOnlyList<FileChangePreview>> PatchPreviewAsync(ParsedPatch patch) =>
    [
        .. patch.Edits.Count == 0 ? [] : EditPreviews(await planner.PlanAsync(patch.Edits)),
        .. patch.Additions.Select(addition => CreatePreview(addition.Path, addition.Content)),
    ];

    private FileChangePreview CreatePreview(string path, string content)
    {
        EnsureCanCreate(path);
        return new FileChangePreview(ProposedChangeKind.Create, workspace.RelativePath(WorkspacePaths.Resolve(workspace, path)), string.Empty, content);
    }

    private static FileChangePreview[] EditPreviews(IReadOnlyList<PlannedFileEdit> plans) =>
        [.. plans.Select(plan => new FileChangePreview(ProposedChangeKind.Edit, plan.RelativePath, plan.OldText, plan.NewText))];

    private async Task<string> ApplyEditsAsync(
        [Description("Edits to apply.")] FileEdit[] edits,
        [Description("Optional path for the edits that don't set their own (all edits in one file).")] string? path = null)
    {
        var warnings = new List<string>();
        return Report(await ApplyPlansAsync(await planner.PlanAsync(WithPath(edits, path)), warnings), [], warnings);
    }

    private async Task<string> ApplyPatchAsync([Description("The patch text, from '*** Begin Patch' to '*** End Patch'.")] string input)
    {
        var patch = PatchParser.Parse(input);
        var plans = patch.Edits.Count == 0 ? [] : await planner.PlanAsync(patch.Edits);
        foreach (var addition in patch.Additions)
        {
            await CreateAsync(addition.Path, addition.Content);
        }

        var warnings = new List<string>();
        return Report(await ApplyPlansAsync(plans, warnings), [.. patch.Additions.Select(addition => addition.Path)], warnings);
    }

    private async Task<IReadOnlyList<PlannedFileEdit>> ApplyPlansAsync(IReadOnlyList<PlannedFileEdit> plans, List<string> warnings)
    {
        foreach (var plan in plans)
        {
            var tab = await OpenAsync(plan.FullPath);
            Task? saving = null;
            await dispatcher.InvokeAsync(() =>
            {
                // The user may have typed meanwhile: plan offsets are valid only for the same text.
                if (tab.Document.Buffer.GetText() != plan.OldText)
                {
                    throw new AgentToolException(Format(Strings.FileChangedDuringEdit, plan.RelativePath));
                }

                tab.Document.Buffer.ReplaceAll(plan.Replacements);
                saving = documents.SaveAsync(tab.Document);
            });
            fileState.RecordWrite(plan.FullPath, plan.NewText, plan.OldText);
            try
            {
                await saving!;
            }
            catch (IOException exception)
            {
                warnings.Add(string.Format(CultureInfo.CurrentCulture, Strings.EditNotSaved, plan.RelativePath, exception.Message));
            }
        }

        return plans;
    }

    private static string Report(IReadOnlyList<PlannedFileEdit> plans, IReadOnlyList<string> created, IReadOnlyList<string> saveWarnings)
    {
        var changed = plans.Select(plan => plan.RelativePath).ToList();
        var warnings = plans.SelectMany(plan => plan.Warnings).Concat(saveWarnings).ToList();
        var text = new StringBuilder();
        if (changed.Count > 0)
        {
            text.Append(Format(Strings.FilesEdited, string.Join(", ", changed)));
        }

        if (created.Count > 0)
        {
            text.Append((text.Length > 0 ? " " : string.Empty) + Format(Strings.FilesCreated, string.Join(", ", created)));
        }

        return text + (warnings.Count == 0 ? string.Empty : "\n" + string.Join('\n', warnings));
    }

    private async Task<string> CreateFileAsync(
        [Description("New file path relative to the workspace root.")] string path,
        [Description("Full text of the file.")] string content) =>
        Format(Strings.FileCreated, await CreateAsync(path, content));

    /// <returns>The new file's workspace-relative path.</returns>
    private async Task<string> CreateAsync(string path, string content)
    {
        var full = EnsureCanCreate(path);
        var folder = Path.GetDirectoryName(full)!;
        if (!fileSystem.DirectoryExists(folder))
        {
            fileSystem.CreateDirectory(folder);
        }

        fileSystem.WriteAllBytesAtomic(full, Encoding.UTF8.GetBytes(content));
        fileState.RecordWrite(full, content, previousText: null);
        await OpenAsync(full);
        return workspace.RelativePath(full);
    }

    private string EnsureCanCreate(string path)
    {
        var full = WorkspacePaths.Resolve(workspace, path);
        SensitivePaths.EnsureWritable(workspace.RelativePath(full));
        if (fileSystem.FileExists(full) || fileSystem.DirectoryExists(full))
        {
            throw new AgentToolException(Format(Strings.FileAlreadyExists, path));
        }

        return full;
    }

    // Tabs open on the UI thread; OpenTextAsync's continuation runs there too.
    private async Task<EditorTabViewModel> OpenAsync(string path)
    {
        Task<EditorTabViewModel?>? opening = null;
        await dispatcher.InvokeAsync(() => opening = editors.OpenTextAsync(new OpenFileRequest(path)));
        return await opening! ?? throw new AgentToolException(Format(Strings.CannotOpenFile, workspace.RelativePath(path)));
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
