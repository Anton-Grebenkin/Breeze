using CodeEditor.Core.Modules;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.ViewModels;
using CodeEditor.Modules.Search.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Search.Wpf;

/// <summary>Search module views: maps <see cref="SearchViewModel"/> to <see cref="SearchView"/>.</summary>
public sealed class SearchWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("search.wpf", Strings.ViewModuleName)
    {
        Dependencies = [SearchModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Contribute(IServiceProvider services) =>
        services.GetRequiredService<IViewRegistry>().Register<SearchViewModel, SearchView>();
}
