using CodeEditor.Core.Modules;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Modules.Git.ViewModels.Tabs;
using CodeEditor.Modules.Git.Wpf.Views;
using CodeEditor.Shell.Wpf;
using CodeEditor.Shell.Wpf.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Git.Wpf;

/// <summary>git views (ADR 0032): the Source Control panel, plus file diff and history as editor tabs.</summary>
public sealed class GitWpfModule : IModule
{
    public ModuleInfo Info { get; } = new("git.wpf", Strings.ViewModuleName)
    {
        Dependencies = [GitModule.Id, ShellWpfModule.Id],
    };

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Contribute(IServiceProvider services)
    {
        var views = services.GetRequiredService<IViewRegistry>();
        views.Register<GitPanelViewModel, GitPanelView>();
        views.Register<GitDiffViewModel, GitDiffView>();
        views.Register<GitHistoryViewModel, GitHistoryView>();
    }
}
