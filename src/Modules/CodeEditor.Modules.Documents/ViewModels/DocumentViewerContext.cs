using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Documents.ViewModels;

/// <summary>Services for document viewers: file system, folder watching, external app, UI thread, time, commands.</summary>
public sealed class DocumentViewerContext(
    IFileSystem fileSystem, IWorkspace workspace, ISystemShell shell, IUiDispatcher dispatcher, TimeProvider time, ICommandService commands)
{
    public IFileSystem FileSystem { get; } = fileSystem;

    public IWorkspace Workspace { get; } = workspace;

    /// <summary>Opens files in an external app.</summary>
    public ISystemShell Shell { get; } = shell;

    public IUiDispatcher Dispatcher { get; } = dispatcher;

    public TimeProvider Time { get; } = time;

    public ICommandService Commands { get; } = commands;
}
