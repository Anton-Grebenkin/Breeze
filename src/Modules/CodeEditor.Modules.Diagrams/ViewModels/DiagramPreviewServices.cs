using CodeEditor.Core.Commands;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Shell.Theming;

namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>
/// Preview dependencies in one object: the module creates tabs on command, each tab has its own file path, and the
/// services are shared.
/// </summary>
public sealed record DiagramPreviewServices(
    IDiagramRenderer Renderer,
    IDocumentService Documents,
    DiagramTextReader Reader,
    IWorkspace Workspace,
    IUiDispatcher Dispatcher,
    IThemeService Themes,
    TimeProvider Time,
    DiagramExports Exports,
    ICommandService Commands);
