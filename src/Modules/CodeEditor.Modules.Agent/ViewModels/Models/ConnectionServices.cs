using CodeEditor.Core.Settings;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>What the connections panel and its rows work with.</summary>
public sealed record ConnectionServices(
    ConnectionStore Store,
    IConnectionProbe Probe,
    IDialogService Dialogs,
    ISettingsService Settings,
    IOptionsMonitor<AgentOptions> Options,
    StatusBarViewModel StatusBar,
    IModelProbe ModelProbe);
