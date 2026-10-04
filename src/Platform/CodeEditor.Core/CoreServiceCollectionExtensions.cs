using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Processes;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers core services: context, commands, keybindings, workspace, documents and settings.</summary>
    public static IServiceCollection AddCodeEditorCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContextKeyService, ContextKeyService>();
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<ICommandService, CommandService>();
        services.AddSingleton<IKeybindingRegistry, KeybindingRegistry>();
        services.AddSingleton<KeyCaptures>();
        services.AddSingleton<KeybindingResolver>();
        services.AddSingleton<UserKeybindings>();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        services.AddSingleton<UserDataPaths>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IWorkspace, Workspace>();
        services.AddSingleton<IFileIndex, FileIndex>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSettingsSection<FilesOptions>(FilesOptions.Section);
        services.AddSingleton<ExcludeSettings>();
        services.AddSettingsSection<LoggingOptions>(LoggingOptions.Section);
        services.AddSingleton<LogLevelSettings>();
        return services;
    }
}
