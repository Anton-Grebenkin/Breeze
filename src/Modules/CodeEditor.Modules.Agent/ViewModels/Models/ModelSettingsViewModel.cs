using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// The model and its parameters below the chat input: a palette model picker and a parameter menu (reasoning,
/// temperature, output length, context window, edit approvals, service, roles) with the current choice checked. Values
/// go to the user's <c>settings.json</c> and apply to all folders; "Default" removes the key.
/// </summary>
public sealed partial class ModelSettingsViewModel : ObservableObject, IDisposable
{
    public const string ModelKey = "agent.model";
    public const string ReasoningKey = "agent.reasoningEffort";
    public const string TemperatureKey = "agent.temperature";
    public const string MaxOutputTokensKey = "agent.maxOutputTokens";
    public const string ContextWindowKey = "agent.contextWindow";
    public const string ApprovalsKey = "agent.approvals";
    public const string HelperModelKey = "agent.helperModel";
    public const string ExplorerModelKey = "agent.explorerModel";
    public const string ExplorerReasoningKey = "agent.explorerReasoningEffort";
    public const string AdvisorModelKey = "agent.advisorModel";
    public const string AdvisorReasoningKey = "agent.advisorReasoningEffort";

    // Roles have their own defaults (advisor high, explorer low), so "Default" is not offered.
    private static readonly AgentReasoningEffort[] RoleReasoningChoices =
    [
        AgentReasoningEffort.Low,
        AgentReasoningEffort.Medium,
        AgentReasoningEffort.High,
        AgentReasoningEffort.ExtraHigh,
    ];

    private static string DefaultTitle => Strings.ParameterDefault;

    private static string SameAsAgentTitle => Strings.SameAsAgent;

    private static readonly AgentReasoningEffort[] ReasoningChoices =
    [
        AgentReasoningEffort.Default,
        AgentReasoningEffort.None,
        AgentReasoningEffort.Low,
        AgentReasoningEffort.Medium,
        AgentReasoningEffort.High,
        AgentReasoningEffort.ExtraHigh,
    ];

    private static readonly double?[] Temperatures = [null, 0, 0.2, 0.5, 0.7, 1, 1.5];
    private static readonly int?[] OutputLimits = [null, 1_024, 4_096, 8_192, 16_384, 32_768, 65_536];
    private static readonly int?[] ContextWindows = [null, 32_000, 128_000, 200_000, 400_000, 1_000_000];

    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly ISettingsService _settings;
    private readonly IQuickPick _quickPick;
    private readonly ICommandService _commands;
    private readonly StatusBarViewModel _statusBar;
    private readonly ServiceMenu _services;
    private readonly IDisposable? _subscription;

    public ModelSettingsViewModel(
        IOptionsMonitor<AgentOptions> options,
        ISettingsService settings,
        IQuickPick quickPick,
        ICommandService commands,
        StatusBarViewModel statusBar,
        ModelManagerViewModel manager)
    {
        Manager = manager;
        _options = options;
        _settings = settings;
        _quickPick = quickPick;
        _commands = commands;
        _statusBar = statusBar;
        _services = new ServiceMenu(settings, options, statusBar, AgentServices.Selectable);
        ParameterMenu = BuildMenu();
        _subscription = options.OnChange(_ => Refresh());
    }

    public string Model => _options.CurrentValue.Model;

    /// <summary>The model name without the vendor, for the button.</summary>
    public string ModelShortName => ModelVendors.ShortName(Model);

    /// <summary>Chooses which models appear in the picker.</summary>
    public ModelManagerViewModel Manager { get; }

    /// <summary>The parameter menu is open, via the button or the palette command; navigated with arrow keys.</summary>
    [ObservableProperty]
    public partial bool IsParametersOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParametersSummary))]
    public partial IReadOnlyList<MenuItemViewModel> ParameterMenu { get; private set; }

    /// <summary>Parameters button tooltip: the checked choice of each group; action items are skipped.</summary>
    public string ParametersSummary => string.Format(CultureInfo.CurrentCulture, Strings.ParametersSummary, string.Join(", ", ParameterMenu
        .Where(group => group.Items.Any(item => item.IsChecked))
        .Select(group => $"{group.Header.ToLowerInvariant()}: {group.Items.First(item => item.IsChecked).Header.ToLowerInvariant()}")));

    public void Dispose() => _subscription?.Dispose();

    // "OpenAI · optimal · current": vendor, tier for supported models (ADR 0017), and a mark on the current one.
    private string ModelDetail(string model)
    {
        List<string> parts = [ModelVendors.Title(ModelVendors.VendorOf(model))];
        if (ModelCatalog.SupportedFor(model) is { } supported)
        {
            parts.Add(supported.Tier switch
            {
                ModelTier.Fast => Strings.ModelTierFast,
                ModelTier.Powerful => Strings.ModelTierPowerful,
                _ => Strings.ModelTierOptimal,
            });
        }

        if (string.Equals(model, Model, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(Strings.CurrentModel);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Picks a model in the palette from selectable vendors only (<see cref="ModelCatalog.Choices"/>): the model manager
    /// and free-form input are hidden while prompts are tuned for a fixed set of models.
    /// </summary>
    [RelayCommand]
    private void PickModel()
    {
        IsParametersOpen = false;
        var items = ModelCatalog.Choices(_options.CurrentValue).Select(model => new QuickPickItem(model, model) { Detail = ModelDetail(model) }).ToList();
        _quickPick.Show(new QuickPickProvider(Strings.PickModelPrompt, items, item =>
        {
            Write(ModelKey, item.Id, string.Format(CultureInfo.CurrentCulture, Strings.ModelChanged, item.Id));
            return Task.CompletedTask;
        }));
    }

    /// <summary>The helper model summarizes on context compaction; a fast, cheap one fits.</summary>
    [RelayCommand]
    private void PickHelperModel() =>
        PickSecondaryModel(HelperModelKey, _options.CurrentValue.HelperModel, Strings.HelperModelPrompt, Strings.HelperModel);

    /// <summary>The explorer model studies code in its own context (ADR 0012).</summary>
    [RelayCommand]
    private void PickExplorerModel() =>
        PickSecondaryModel(ExplorerModelKey, _options.CurrentValue.ExplorerModel, Strings.ExplorerModelPrompt, Strings.ExplorerModel);

    /// <summary>The Deep mode advisor and reviewer; a strong model from another vendor works best (ADR 0012).</summary>
    [RelayCommand]
    private void PickAdvisorModel() =>
        PickSecondaryModel(AdvisorModelKey, _options.CurrentValue.AdvisorModel, Strings.AdvisorModelPrompt, Strings.AdvisorModel);

    /// <summary>Picks a secondary model from the selectable ones, or "same as agent", which removes the key.</summary>
    private void PickSecondaryModel(string key, string? current, string title, string status)
    {
        IsParametersOpen = false;
        List<QuickPickItem> items =
        [
            new(string.Empty, SameAsAgentTitle) { Detail = string.IsNullOrWhiteSpace(current) ? Strings.CurrentModel : Model },
            .. ModelCatalog.Choices(_options.CurrentValue)
                .Select(model => new QuickPickItem(model, model) { Detail = string.Equals(model, current, StringComparison.OrdinalIgnoreCase) ? Strings.CurrentModel : null }),
        ];
        _quickPick.Show(new QuickPickProvider(title, items, item =>
        {
            var model = item.Id.Length == 0 ? null : item.Id;
            Write(key, model, $"{status}: {model ?? SameAsAgentTitle.ToLowerInvariant()}");
            return Task.CompletedTask;
        }));
    }

    [RelayCommand]
    private void ShowParameters() => IsParametersOpen = true;

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        IsParametersOpen = false;
        await _commands.ExecuteAsync(SettingsCommands.OpenUserSettingsId);
    }

    private void Refresh()
    {
        ParameterMenu = BuildMenu();
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(ModelShortName));
    }

    private List<MenuItemViewModel> BuildMenu()
    {
        var options = _options.CurrentValue;
        return
        [
            Group(Strings.ParamReasoning, ReasoningKey, ReasoningChoices.Select(choice =>
                (ReasoningTitle(choice), (object?)ReasoningSetting(choice), choice == options.ReasoningEffort))),
            Group(Strings.ParamTemperature, TemperatureKey, WithCurrent(Temperatures, options.Temperature).Select(value =>
                (value is { } number ? number.ToString("0.0#", CultureInfo.CurrentCulture) : DefaultTitle, (object?)value, value == options.Temperature))),
            Group(Strings.ParamMaxOutputTokens, MaxOutputTokensKey, WithCurrent(OutputLimits, options.MaxOutputTokens).Select(value =>
                (TokensTitle(value, DefaultTitle), (object?)value, value == options.MaxOutputTokens))),
            Group(Strings.ParamContextWindow, ContextWindowKey, WithCurrent(ContextWindows, options.ContextWindow).Select(value =>
                (TokensTitle(value, Strings.ParameterAuto), (object?)value, value == options.ContextWindow))),
            Group(Strings.ParamApprovals, ApprovalsKey,
            [
                (Strings.ApproveEachEdit, (object?)"edits", options.Approvals == AgentApprovals.Edits),
                (Strings.AcceptEditsImmediately, (object?)"auto", options.Approvals == AgentApprovals.Auto),
            ]),
            .. _services.Build(),
            Roles(options),
            MenuItemViewModel.CreateSeparator(),
            MenuItemViewModel.ForAction(Strings.PickModelAction, "Agent.Parameters.PickModel", new AsyncRelayCommand(() => Run(PickModel))),
            MenuItemViewModel.ForAction(Strings.AllSettingsAction, "Agent.Parameters.OpenSettings", OpenSettingsCommand),
        ];
    }

    // Roles (ADR 0012): explorer model and reasoning, helper model for summaries. The advisor belongs to Deep mode,
    // which is hidden from the UI (AgentModes.Selectable), so its model choice is hidden too.
    private MenuItemViewModel Roles(AgentOptions options) => MenuItemViewModel.ForGroup(Strings.ParamRoles, "Agent.Parameters.Roles",
    [
        MenuItemViewModel.ForAction(Strings.ExplorerModelAction, "Agent.Parameters.PickExplorerModel", new AsyncRelayCommand(() => Run(PickExplorerModel))),
        Group(Strings.ParamExplorerReasoning, ExplorerReasoningKey, RoleReasoningChoices.Select(choice =>
            (ReasoningTitle(choice), (object?)ReasoningSetting(choice), choice == options.ExplorerReasoningEffort))),
        MenuItemViewModel.CreateSeparator(),
        MenuItemViewModel.ForAction(Strings.HelperModelAction, "Agent.Parameters.PickHelperModel", new AsyncRelayCommand(() => Run(PickHelperModel))),
    ]);

    private MenuItemViewModel Group(string title, string key, IEnumerable<(string Title, object? Setting, bool IsCurrent)> choices)
    {
        var groupId = "Agent.Parameters." + key;
        var items = choices
            .Select((choice, index) => MenuItemViewModel.ForAction(
                choice.Title,
                $"{groupId}.{index}",
                new AsyncRelayCommand(() => Run(() => Write(key, choice.Setting, $"{title}: {choice.Title.ToLowerInvariant()}"))),
                choice.IsCurrent))
            .ToList();
        return MenuItemViewModel.ForGroup(title, groupId, items);
    }

    private void Write(string key, object? value, string description)
    {
        _statusBar.Message = _settings.TrySetUserValue(key, value, out var error) ? description : error;
    }

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static string ReasoningTitle(AgentReasoningEffort value) => value switch
    {
        AgentReasoningEffort.None => Strings.ReasoningNone,
        AgentReasoningEffort.Low => Strings.ReasoningLow,
        AgentReasoningEffort.Medium => Strings.ReasoningMedium,
        AgentReasoningEffort.High => Strings.ReasoningHigh,
        AgentReasoningEffort.ExtraHigh => Strings.ReasoningExtraHigh,
        _ => DefaultTitle,
    };

    private static string? ReasoningSetting(AgentReasoningEffort value) =>
        value == AgentReasoningEffort.Default ? null : JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    // The exact number with group separators ("8,192"), not a rounded "8.2K".
    private static string TokensTitle(int? value, string defaultTitle) =>
        value is { } tokens ? tokens.ToString("N0", CultureInfo.CurrentCulture) : defaultTitle;

    // A value set by hand in settings.json is listed too, checked.
    private static IEnumerable<T> WithCurrent<T>(T[] values, T current) => values.Contains(current) ? values : [.. values, current];
}
