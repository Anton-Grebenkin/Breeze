using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Shell.Services;
using CodeEditor.Modules.Viewers.Services;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Viewer services: files, bytes for the dump, image decoder, folder watching, external app, UI thread, time, commands.
/// </summary>
public sealed class ViewerContext(
    IFileSystem fileSystem,
    IFileBytes bytes,
    IImageDecoder decoder,
    IWorkspace workspace,
    ISystemShell shell,
    IUiDispatcher dispatcher,
    TimeProvider time,
    ICommandService commands)
{
    public IFileSystem FileSystem { get; } = fileSystem;

    public IFileBytes Bytes { get; } = bytes;

    public IImageDecoder Decoder { get; } = decoder;

    public IWorkspace Workspace { get; } = workspace;

    /// <summary>Opens a file in an external app.</summary>
    public ISystemShell Shell { get; } = shell;

    public IUiDispatcher Dispatcher { get; } = dispatcher;

    public TimeProvider Time { get; } = time;

    public ICommandService Commands { get; } = commands;
}
