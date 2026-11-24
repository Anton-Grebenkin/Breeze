using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Output;

/// <summary>
/// Output channels, like the Output panel in VS Code and Visual Studio. Modules write to their own channel
/// without knowing how it is displayed.
/// </summary>
public interface IOutputService
{
    /// <summary>The app log channel the logger writes to. Named in the UI language.</summary>
    static string LogChannelName => Strings.OutputLogChannel;

    event EventHandler? ChannelsChanged;

    IReadOnlyList<IOutputChannel> Channels { get; }

    IOutputChannel GetOrCreate(string name);

    /// <summary>
    /// Asks the panel to come forward with this channel: a failed build the user started is worth looking at. Nothing
    /// happens where there is no panel.
    /// </summary>
    void Reveal(IOutputChannel channel)
    {
    }
}
