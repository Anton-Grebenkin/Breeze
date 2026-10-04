using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Documents.Services;
using CodeEditor.Modules.Documents.Services.Changes;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Documents.Tests;

/// <summary>In-memory <c>C:\work</c> workspace with document tools on top; executed commands are recorded.</summary>
internal sealed class DocumentsFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\work");

    public DocumentsFixture()
    {
        Workspace = new Workspace(Files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        Workspace.Open(Root);
        var files = new DocumentFiles(Workspace, Files);
        Tools = new DocumentAgentTools(files, new DocumentReading(files), new DocumentChanges(files), new PassThroughOutputStore(), Commands, new InlineUiDispatcher());
    }

    public FakeFileSystem Files { get; } = new FakeFileSystem().AddDirectory(Root);

    public Workspace Workspace { get; }

    public RecordingCommands Commands { get; } = new();

    public DocumentAgentTools Tools { get; }

    public static string PathOf(string name) => Path.Combine(Root, name);

    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public async Task<string> InvokeAsync(string tool, Dictionary<string, object?> arguments)
    {
        var function = Tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == tool);
        return (await function.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }

    public Task<string> ReadAsync(string path, Dictionary<string, object?>? more = null) =>
        InvokeAsync(DocumentAgentTools.ReadName, new Dictionary<string, object?>(more ?? []) { ["path"] = path });

    public Task<string> ChangeAsync(string action, string path, Dictionary<string, object?>? more = null) =>
        InvokeAsync(DocumentAgentTools.ChangeName, new Dictionary<string, object?>(more ?? []) { ["action"] = action, ["path"] = path });

    public async Task<FileChangePreview> PreviewAsync(string action, string path, Dictionary<string, object?>? more = null) =>
        Assert.Single(await Tools.PreviewAsync(DocumentAgentTools.ChangeName, new Dictionary<string, object?>(more ?? []) { ["action"] = action, ["path"] = path }, TestContext.Current.CancellationToken));

    public void Dispose() => Workspace.Dispose();

    /// <summary>Returns long output as is: output files are tested in the agent module.</summary>
    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }

    internal sealed class RecordingCommands : ICommandService
    {
        public List<(string Id, object? Argument)> Executed { get; } = [];

        public bool CanExecute(string commandId) => true;

        public ValueTask<CommandExecutionStatus> ExecuteAsync(string commandId, object? argument = null, CancellationToken cancellationToken = default)
        {
            Executed.Add((commandId, argument));
            return ValueTask.FromResult(CommandExecutionStatus.Succeeded);
        }
    }
}
