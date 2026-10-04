using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// The "Service" group in model parameters, built from <see cref="AgentServices.Selectable"/>; absent with a single
/// service. Choosing a service writes its endpoint, and also its default model when the current one is not offered
/// there: model names differ between services.
/// </summary>
internal sealed class ServiceMenu(ISettingsService settings, IOptionsMonitor<AgentOptions> options, StatusBarViewModel statusBar, IReadOnlyList<AgentService> services)
{
    public const string EndpointKey = "agent.endpoint";

    /// <returns>The "Service" group, or empty when there is nothing to choose.</returns>
    public IReadOnlyList<MenuItemViewModel> Build() => services.Count < 2 ? [] :
    [
        MenuItemViewModel.ForGroup(Strings.ParamService, "Agent.Parameters.Service",
        [
            .. services.Select((service, index) => MenuItemViewModel.ForAction(
                service.Title,
                $"Agent.Parameters.Service.{index}",
                new AsyncRelayCommand(() =>
                {
                    Use(service);
                    return Task.CompletedTask;
                }),
                AgentServices.For(options.CurrentValue.Endpoint) == service)),
        ]),
    ];

    public void Use(AgentService service)
    {
        var model = options.CurrentValue.Model;
        if (!settings.TrySetUserValue(EndpointKey, service.Endpoint, out var error)
            || (!service.Models.Contains(model, StringComparer.OrdinalIgnoreCase)
                && !settings.TrySetUserValue(ModelSettingsViewModel.ModelKey, service.Models[0], out error)))
        {
            statusBar.Message = error;
            return;
        }

        statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.ServiceChanged, service.Title);
    }
}
