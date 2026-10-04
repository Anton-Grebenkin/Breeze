using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Activity;

/// <summary>
/// Consecutive reads and searches as one row: "Explored 3 files, 2 searches", or "Exploring…" with the current step
/// while they run. Expands on click.
/// </summary>
public sealed partial class ExplorationGroupViewModel : ActivityItemViewModel
{
    public ExplorationGroupViewModel(IEnumerable<ActivityEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            Add(entry);
        }
    }

    public ObservableCollection<ActivityEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public bool IsRunning => Entries.Any(entry => entry.Message.IsInProgress);

    public string Title => IsRunning ? Strings.Exploring : string.Format(CultureInfo.CurrentCulture, Strings.Explored, Summary());

    /// <summary>The title of the step that is running now.</summary>
    public string? Current => Entries.LastOrDefault(entry => entry.Message.IsInProgress)?.Message.Text;

    public void Add(ActivityEntryViewModel entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entries.Add(entry);
        entry.Message.PropertyChanged += OnEntryChanged;
        Refresh();
    }

    public bool Remove(ChatMessageViewModel message)
    {
        if (Entries.FirstOrDefault(entry => entry.Contains(message)) is not { } entry)
        {
            return false;
        }

        entry.Message.PropertyChanged -= OnEntryChanged;
        Entries.Remove(entry);
        Refresh();
        return true;
    }

    public override bool Contains(ChatMessageViewModel message) => Entries.Any(entry => entry.Contains(message));

    public override void Detach()
    {
        foreach (var entry in Entries)
        {
            entry.Message.PropertyChanged -= OnEntryChanged;
        }
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessageViewModel.IsInProgress) or nameof(ChatMessageViewModel.Text))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Current));
    }

    private string Summary()
    {
        var icons = Entries.Select(entry => entry.Message.Tool?.Icon ?? AgentToolIcon.Other).ToList();
        var files = icons.Count(icon => icon == AgentToolIcon.Read);
        var searches = icons.Count(icon => icon == AgentToolIcon.Search);
        var folders = icons.Count(icon => icon == AgentToolIcon.Folder);
        var other = icons.Count - files - searches - folders;
        string?[] parts =
        [
            files > 0 ? Plural.Format(files, Strings.FileForms) : null,
            searches > 0 ? Plural.Format(searches, Strings.SearchForms) : null,
            folders > 0 ? Plural.Format(folders, Strings.FolderForms) : null,
            other > 0 ? Plural.Format(other, Strings.StepForms) : null,
        ];
        return string.Join(", ", parts.OfType<string>());
    }
}
