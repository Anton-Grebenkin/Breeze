using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Web;
using CodeEditor.Modules.Browser.Commands;
using CodeEditor.Modules.Browser.Resources;
using CodeEditor.Modules.Browser.Services;
using CodeEditor.Modules.Browser.Services.Agent;
using CodeEditor.Modules.Browser.Services.Tabs;
using CodeEditor.Modules.Browser.Services.Web;
using CodeEditor.Modules.Browser.ViewModels;
using CodeEditor.Shell;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Browser;

/// <summary>
/// Browser module (ADR 0027, 0054): a real browser with tabs in a tab next to files, as in Cursor (ADR 0031; it can
/// move to any area), and the agent's <c>browser</c> tool. The engine (<see cref="IBrowserEngine"/>) comes from the
/// module's view; a page loads when its tab is first shown, by the user or by an agent call. The "Show: Browser" command and the View
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
        services.AddSettingsSection<BrowserOptions>(BrowserOptions.Section, SettingScope.User);
        services.AddUserOnlySetting(BrowserApprovals.AllowedHostsKey);
        services.AddUserOnlySetting(BrowserApprovals.ActHostsKey);
        services.AddUserOnlySetting(BrowserTabsSwitch.ShareKey);
        services.AddSingleton<BrowserHistory>();
        services.AddSingleton<BrowserTabStore>();
        services.AddSingleton<BrowserTabs>();
        services.AddSingleton<BrowserKnownSites>();
        services.AddSingleton<BrowserViewModel>();
        services.AddSingleton(provider => new BrowserAgentTabs(provider.GetRequiredService<BrowserTabs>(), ShowPanel(provider)));
        services.AddSingleton<IWebBrowserAccess>(provider => new BrowserWebAccess(
            provider.GetRequiredService<BrowserTabs>(), provider.GetRequiredService<IOptionsMonitor<BrowserOptions>>(), provider.GetService<INotificationService>(), ShowPanel(provider)));
        services.AddSingleton<BrowserAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<BrowserAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<BrowserAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy, BrowserApprovals>();
        services.AddSingleton<IAgentToolPresenter, BrowserToolPresenter>();
        services.AddSingleton<IWebPageOpener, BrowserPageOpener>();
        services.AddSingleton<BrowserTabsSwitch>();
        services.AddSingleton<IAgentContextSwitch>(provider => provider.GetRequiredService<BrowserTabsSwitch>());
        services.AddSingleton<IAgentContextProvider, BrowserAgentContext>();
    }

    // The panel comes to the front without focus: the chat box keeps it.
    private static Func<Task> ShowPanel(IServiceProvider provider) => () =>
        provider.GetRequiredService<IUiDispatcher>().InvokeAsync(() => provider.GetRequiredService<WorkbenchLayout>().Show(ToolWindowId, focus: false));

    public void Contribute(IServiceProvider services)
    {
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.ModuleName, IconNames.Browser, ToolWindowLocation.Editor, services.GetRequiredService<BrowserViewModel>)
        {
            Order = 10,
        }));
        // Made now, not on first use: it remembers the sites the user opens whether or not the agent was ever called.
        services.GetRequiredService<BrowserKnownSites>();
        _registrations.AddRange(new BrowserCommands(services.GetRequiredService<BrowserViewModel>, services.GetRequiredService<BrowserTabsSwitch>(), services.GetRequiredService<WorkbenchLayout>())
            .Register(services.GetRequiredService<ICommandRegistry>(), services.GetRequiredService<IKeybindingRegistry>()));
    }
}
