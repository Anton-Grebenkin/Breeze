using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Menus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// A model of a connection: a switch that puts it into the model picker, and for a switched-on one what the user says
/// about it — the name to show, which model it really is, its window, whether it sees images. Every change is saved at
/// once.
/// </summary>
public sealed partial class ConnectionModelRowViewModel : ObservableObject
{
    private static readonly ModelFamily?[] Families =
    [
        null, ModelFamily.Claude, ModelFamily.GptReasoning, ModelFamily.Gpt, ModelFamily.Gemini, ModelFamily.DeepSeek, ModelFamily.Qwen,
        ModelFamily.Frontier, ModelFamily.Other,
    ];

    private readonly ServiceModel _service;
    private readonly ConnectionModelActions _actions;
    private readonly ModelDescription? _described;
    private readonly bool _isReady;
    private ReasoningSource? _reasoning;
    private ModelProbeReport? _checked;
    private CancellationTokenSource? _checking;

    /// <param name="described">What the settings say about the model; <c>null</c> when it is not in the picker.</param>
    public ConnectionModelRowViewModel(ServiceModel service, ModelDescription? described, bool isCurrent, ConnectionModelActions actions)
    {
        ArgumentNullException.ThrowIfNull(service);
        (_service, _described, _actions, _reasoning) = (service, described, actions, described?.Reasoning);
        IsCurrent = isCurrent;
        IsEnabled = described is not null;
        Name = described?.Name ?? string.Empty;
        Family = described?.Family;
        ContextWindow = (described?.ContextWindow ?? service.ContextWindow)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Images = described?.Images ?? service.Images ?? ModelProfiles.For(service.Id).SeesImages;
        _isReady = true;
    }

    /// <summary>The model id the service expects.</summary>
    public string Id => _service.Id;

    /// <summary>The model is in the model picker.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FamilyTitle))]
    public partial ModelFamily? Family { get; set; }

    /// <summary>Context window in tokens, as typed; empty to guess it from the id.</summary>
    [ObservableProperty]
    public partial string ContextWindow { get; set; }

    [ObservableProperty]
    public partial bool Images { get; set; }

    /// <summary>It is the conversation model: it stays in the picker.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public string FamilyTitle => Title(Family);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool IsChecking { get; private set; }

    /// <summary>What the last check found, a line per probe; empty before the first check.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ModelProbeLine> Report { get; private set; } = [];

    /// <summary>The check found something the settings do not say yet.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCheckCommand))]
    public partial bool CanApplyCheck { get; private set; }

    /// <summary>The family choice, opened under its button.</summary>
    public IReadOnlyList<MenuItemViewModel> FamilyMenu =>
    [
        .. Families.Select((family, index) => MenuItemViewModel.ForAction(
            Title(family), $"Agent.Connections.FamilyChoice.{index}", new AsyncRelayCommand(() => Choose(family)), family == Family)),
    ];

    /// <summary>What goes into the settings; <c>null</c> for a model that is not in the picker.</summary>
    public ModelDescription? ToDescription() => !IsEnabled ? null : new ModelDescription
    {
        Id = Id,
        Name = Name.Trim() is { Length: > 0 } name ? name : null,
        Family = Family,
        ContextWindow = int.TryParse(ContextWindow.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var window) && window > 0 ? window : null,
        Images = Images,

        // Set by hand in the settings file: the row does not edit them and must not drop them.
        MaxOutputTokens = _described?.MaxOutputTokens,
        Reasoning = _reasoning,
        EditFormat = _described?.EditFormat,
    };

    /// <summary>Asks the model itself what it can do; the report shows under the row.</summary>
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        _checking?.Cancel();
        var checking = _checking = new CancellationTokenSource();
        (IsChecking, Report, CanApplyCheck) = (true, [], false);
        try
        {
            _checked = await _actions.Check(this, checking.Token);
            Report = [.. _checked.Steps.Select(static step => new ModelProbeLine(step))];
            CanApplyCheck = _checked.HasSuggestions;
        }
        catch (OperationCanceledException) when (checking.IsCancellationRequested)
        {
            _checked = null;
        }
        finally
        {
            if (ReferenceEquals(_checking, checking))
            {
                (_checking, IsChecking) = (null, false);
            }

            checking.Dispose();
        }
    }

    private bool CanCheck() => !IsChecking;

    /// <summary>Writes what the check found into the description; the response limit field belongs to the connection.</summary>
    [RelayCommand(CanExecute = nameof(CanApplyCheck))]
    private void ApplyCheck()
    {
        if (_checked is not { } report)
        {
            return;
        }

        _reasoning = report.Reasoning ?? _reasoning;
        CanApplyCheck = false;
        _actions.Apply(report);
        Images = report.Images ?? Images;
        _actions.Changed(this);
    }

    /// <summary>Puts the model into the picker and makes it the conversation model.</summary>
    [RelayCommand]
    private void Use()
    {
        IsEnabled = true;
        _actions.Use(this);
    }

    private Task Choose(ModelFamily? family)
    {
        Family = family;
        OnPropertyChanged(nameof(FamilyMenu));
        return Task.CompletedTask;
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isReady && e.PropertyName is nameof(IsEnabled) or nameof(Name) or nameof(Family) or nameof(ContextWindow) or nameof(Images))
        {
            _actions.Changed(this);
        }
    }

    private static string Title(ModelFamily? family) => family switch
    {
        ModelFamily.Claude => Strings.FamilyClaude,
        ModelFamily.GptReasoning => Strings.FamilyGptReasoning,
        ModelFamily.Gpt => Strings.FamilyGpt,
        ModelFamily.Gemini => Strings.FamilyGemini,
        ModelFamily.DeepSeek => Strings.FamilyDeepSeek,
        ModelFamily.Qwen => Strings.FamilyQwen,
        ModelFamily.Frontier => Strings.FamilyFrontier,
        ModelFamily.Other => Strings.FamilyOther,
        _ => Strings.FamilyByName,
    };
}
