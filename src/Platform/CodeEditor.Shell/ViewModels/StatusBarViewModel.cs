using System.Collections.ObjectModel;
using CodeEditor.Shell.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Shell.ViewModels;

/// <summary>
/// The main window status bar: a message on the left and module items on the right.
/// </summary>
public sealed partial class StatusBarViewModel : ObservableObject
{
    public static string ReadyMessage => Strings.StatusReady;

    /// <summary>The message on the left.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = ReadyMessage;

    /// <summary>Items on the right, in order.</summary>
    public ObservableCollection<StatusBarItemViewModel> Items { get; } = [];

    /// <summary>Inserts the item by <see cref="StatusBarItemViewModel.Order"/>; dispose the result to remove it.</summary>
    public IDisposable Add(StatusBarItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var position = 0;
        while (position < Items.Count && Items[position].Order <= item.Order)
        {
            position++;
        }

        Items.Insert(position, item);
        return new Removal(this, item);
    }

    private sealed class Removal(StatusBarViewModel owner, StatusBarItemViewModel item) : IDisposable
    {
        public void Dispose() => owner.Items.Remove(item);
    }
}
