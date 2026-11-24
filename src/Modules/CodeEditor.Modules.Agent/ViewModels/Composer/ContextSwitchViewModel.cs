using System.ComponentModel;
using CodeEditor.Modules.Agent.Contracts.Context;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>A module's context chip above the input box (<see cref="IAgentContextSwitch"/>): a click turns it on or off.</summary>
public sealed partial class ContextSwitchViewModel : ObservableObject, IDisposable
{
    private readonly IAgentContextSwitch _source;

    public ContextSwitchViewModel(IAgentContextSwitch source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _source.PropertyChanged += OnSourceChanged;
    }

    public string AutomationId => "Agent.Context." + _source.Id;

    public string Icon => _source.Icon;

    public string Label => _source.Label;

    public string ToolTip => _source.ToolTip;

    public bool IsAvailable => _source.IsAvailable;

    public bool IsOn => _source.IsOn;

    public void Dispose() => _source.PropertyChanged -= OnSourceChanged;

    [RelayCommand]
    private void Toggle() => _source.IsOn = !_source.IsOn;

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);
}
