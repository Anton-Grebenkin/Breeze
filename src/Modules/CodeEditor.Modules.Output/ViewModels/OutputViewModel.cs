using CodeEditor.Core.Output;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Output.ViewModels;

/// <summary>
/// Output panel: channel selection and its text. The view polls <see cref="Read"/> on a timer while visible and keeps
/// its own cursor, so it appends what is new instead of copying the whole channel (up to a million characters) four
/// times a second.
/// </summary>
public sealed partial class OutputViewModel : ObservableObject, IDisposable
{
    private readonly IOutputService _output;

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

    /// <summary>
    /// What the selected channel gained since the reader's cursor: appended text, or the whole text after a clear or a
    /// trim. A reader that showed another channel passes <see cref="OutputCursor.None"/>.
    /// </summary>
    public OutputDelta Read(OutputCursor cursor) =>
        SelectedChannel?.ReadSince(cursor) ?? new OutputDelta(string.Empty, IsReset: cursor != OutputCursor.None, OutputCursor.None);

    [RelayCommand]
    public void Clear() => SelectedChannel?.Clear();

    public void Dispose() => _output.ChannelsChanged -= OnChannelsChanged;

    // May arrive on a background thread: WPF marshals simple property changes to the UI thread.
    private void OnChannelsChanged(object? sender, EventArgs e)
    {
        Channels = _output.Channels;
        SelectedChannel ??= Channels.Count > 0 ? Channels[0] : null;
    }
}
