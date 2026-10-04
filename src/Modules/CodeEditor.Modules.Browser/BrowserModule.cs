using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Modules.Browser;

/// <summary>
/// Browser module (ADR 0027): a real browser in a tab next to files, as in Cursor (ADR 0031; it can move to any area),
/// and the agent's <c>browser</c> tool. The engine (<see cref="IBrowserEngine"/>) comes from the module's view and is
/// created when the panel is first shown, by the user or by an agent call. The "Show: Browser" command and the View
/// menu item come from the panel registration. Pages other modules open (<see cref="IWebPageOpener"/>) open here too.
/// </summary>
public sealed class BrowserModule : IModule
{
    public const string Id = "browser";
    public const string ToolWindowId = "browser";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.ModuleName) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<BrowserOptions>(BrowserOptions.Section);
        services.AddSingleton<BrowserViewModel>();
        services.AddSingleton<BrowserAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<BrowserAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<BrowserAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy, BrowserApprovals>();
        services.AddSingleton<IAgentToolPresenter, BrowserToolPresenter>();
        services.AddSingleton<IWebPageOpener, BrowserPageOpener>();
    }

    public void Contribute(IServiceProvider services) =>
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Browser, ToolWindowLocation.Editor, services.GetRequiredService<BrowserViewModel>)
        {
            Order = 10,
        }));
}
