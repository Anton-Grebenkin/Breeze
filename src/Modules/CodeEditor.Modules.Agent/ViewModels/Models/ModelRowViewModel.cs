using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// A model manager row: a vendor header (collapses the group) or a model with a "show in picker" toggle. The list is
/// flat so it virtualizes as a whole.
/// </summary>
public sealed partial class ModelRowViewModel : ObservableObject
{
    private readonly Action<ModelRowViewModel>? _toggled;

    // The initial state is set before subscribing: creating a row is not a toggle.
    private ModelRowViewModel(string id, string title, bool isVendor, bool isChecked, Action<ModelRowViewModel>? toggled)
    {
        Id = id;
        Title = title;
        IsVendor = isVendor;
        IsChecked = isChecked;
        _toggled = toggled;
    }

    /// <summary><c>vendor/model</c> for a model, the vendor id for a vendor.</summary>
    public string Id { get; }

    public string Title { get; }

    public bool IsVendor { get; }

    /// <summary>For a vendor: "2 of 88 enabled".</summary>
    [ObservableProperty]
    public partial string? Detail { get; set; }

    /// <summary>For a model: shown in the chat picker; for a vendor: the group is expanded.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>The agent's current model cannot be disabled; it is always in the picker.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public static ModelRowViewModel Vendor(string id, string title, bool isExpanded, Action<ModelRowViewModel> toggled) =>
        new(id, title, isVendor: true, isExpanded, toggled);

    public static ModelRowViewModel Model(string id, string title, bool isEnabled, Action<ModelRowViewModel> toggled) =>
        new(id, title, isVendor: false, isEnabled, toggled);

    partial void OnIsCheckedChanged(bool value) => _toggled?.Invoke(this);
}
