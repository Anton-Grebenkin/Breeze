using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Docker.ViewModels.Tree;

/// <summary>
/// A Docker tree node. Refreshes do not recreate nodes: the node with the same <see cref="Key"/> gets new data
/// (<see cref="NodeList"/>), so selection, expansion and scrolling survive and the tree does not flicker.
/// </summary>
public abstract partial class DockerNodeViewModel : ObservableObject
{
    protected DockerNodeViewModel(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Stable node key: "container:&lt;id&gt;", "image:&lt;id&gt;:&lt;name&gt;", "compose:&lt;file&gt;".</summary>
    public string Key { get; }

    public abstract DockerNodeKind Kind { get; }

    public abstract string AutomationId { get; }

    public ObservableCollection<DockerNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    public partial string Title { get; protected set; } = string.Empty;

    /// <summary>Grey text after the title: state, image, size.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail))]
    public partial string Description { get; protected set; } = string.Empty;

    /// <summary>Codicons icon name.</summary>
    [ObservableProperty]
    public partial string Icon { get; protected set; } = string.Empty;

    [ObservableProperty]
    public partial DockerTone Tone { get; protected set; }

    [ObservableProperty]
    public partial string ToolTip { get; protected set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>What is happening to the node now ("stopping…"); <c>null</c> when idle. Refreshes keep it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(Detail))]
    public partial string? BusyText { get; set; }

    public bool IsBusy => BusyText is not null;

    /// <summary>Grey text: the running action, otherwise the description.</summary>
    public string Detail => BusyText ?? Description;

    public override string ToString() => Title;
}
