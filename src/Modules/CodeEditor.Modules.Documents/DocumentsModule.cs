using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Documents.Commands;
using CodeEditor.Modules.Documents.Resources;
using CodeEditor.Modules.Documents.Services;
using CodeEditor.Modules.Documents.Services.Changes;
using CodeEditor.Modules.Documents.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Documents;

/// <summary>
/// Documents module (ADR 0034): PDF, Word, Excel, PowerPoint and CSV. The user gets viewers in editor tabs
/// (<see cref="DocumentViewerProvider"/>); the agent gets <c>document</c> to read without asking and
/// <c>document_change</c> to create and edit through an approval card. Format libraries (Open XML SDK, PdfPig, PDFsharp,
/// MigraDoc) load on the first document open or tool call: registration does not touch them.
/// </summary>
public sealed class DocumentsModule : IModule
{
    public const string Id = "documents";

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<DocumentOptions>(DocumentOptions.Section);
        services.AddSingleton<DocumentViewerContext>();
        services.AddSingleton<IFileViewerProvider, DocumentViewerProvider>();
        services.AddSingleton<DocumentCommands>();
        services.AddSingleton<DocumentFiles>();
        services.AddSingleton<DocumentReading>();
        services.AddSingleton<DocumentChanges>();
        services.AddSingleton<DocumentAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<DocumentAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<DocumentAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy, DocumentApprovals>();
        services.AddSingleton<IAgentToolPresenter, DocumentToolPresenter>();
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<DocumentCommands>().Register(services.GetRequiredService<ICommandRegistry>(), services.GetRequiredService<IMenuRegistry>());
}
