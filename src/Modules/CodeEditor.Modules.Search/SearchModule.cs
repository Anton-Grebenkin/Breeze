using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Search.Commands;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services;
using CodeEditor.Modules.Search.Services.Agent;
using CodeEditor.Modules.Search.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Search;

/// <summary>Search module: workspace file search in the side bar (<c>Ctrl+Shift+F</c>), as in VS Code.</summary>
public sealed class SearchModule : IModule
{
    public const string Id = "search";
    public const string ToolWindowId = "search";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSettingsSection<SearchSettings>(SearchSettings.Section);
        services.AddSingleton<TextSearchService>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<IAgentToolProvider, SearchAgentTools>();
        services.AddSingleton<IAgentContextProvider, RelatedCodeContext>();
        services.AddSingleton<IAgentToolPresenter, SearchToolPresenter>();
        services.AddSingleton(provider => new SearchCommands(provider.GetRequiredService<SearchViewModel>));
    }

    public void Contribute(IServiceProvider services)
    {
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Search, ToolWindowLocation.SideBar, services.GetRequiredService<SearchViewModel>)
        {
            Order = 1,
            Keybinding = "Ctrl+Shift+F",
        }));

        services.GetRequiredService<SearchCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IKeybindingRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
    }
}
