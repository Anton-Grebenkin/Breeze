using CodeEditor.Core.Output;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Output.ViewModels;

/// <summary>
/// Output panel: channel selection and its text. The view calls <see cref="Refresh"/> on a timer while visible;
/// the text is re-read only when the channel changed.
/// </summary>
public sealed partial class OutputViewModel : ObservableObject, IDisposable
{
    private readonly IOutputService _output;
    private long _shownVersion = -1;

    public OutputViewModel(IOutputService output)
    {
        _output = output;
        _output.ChannelsChanged += OnChannelsChanged;
        Channels = _output.Channels;
        SelectedChannel = Channels.Count > 0 ? Channels[0] : null;
    }

    [ObservableProperty]
    public partial IReadOnlyList<IOutputChannel> Channels { get; private set; }

    [ObservableProperty]
    public partial IOutputChannel? SelectedChannel { get; set; }

    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    /// <summary>Re-reads the selected channel if it changed; returns whether the text was updated.</summary>
    public bool Refresh()
    {
        var channel = SelectedChannel;
        if (channel is null || channel.Version == _shownVersion)
        {
            return false;
        }

        _shownVersion = channel.Version;
        Text = channel.Snapshot();
        return true;
    }

    [RelayCommand]
    public void Clear()
    {
        SelectedChannel?.Clear();
        Refresh();
    }

    public void Dispose() => _output.ChannelsChanged -= OnChannelsChanged;

    partial void OnSelectedChannelChanged(IOutputChannel? value)
    {
        _shownVersion = -1;
        Refresh();
    }

    // May arrive on a background thread: WPF marshals simple property changes to the UI thread.
    private void OnChannelsChanged(object? sender, EventArgs e)
    {
        Channels = _output.Channels;
        SelectedChannel ??= Channels.Count > 0 ? Channels[0] : null;
    }
}
