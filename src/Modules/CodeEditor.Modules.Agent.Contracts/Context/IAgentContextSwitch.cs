using System.ComponentModel;

namespace CodeEditor.Modules.Agent.Contracts.Context;

/// <summary>
/// A module's chip above the chat input, next to the open file chip: the user decides whether the agent is told about a
/// kind of editor state (browser tabs). The module's <see cref="IAgentContextProvider"/> honours <see cref="IsOn"/>.
/// </summary>
public interface IAgentContextSwitch : INotifyPropertyChanged
{
    /// <summary>Stable id: the suffix of the chip's AutomationId.</summary>
    string Id { get; }

    /// <summary>Icon name from the editor's icon set.</summary>
    string Icon { get; }

    string Label { get; }

    string ToolTip { get; }

    /// <summary>There is something to report; the chip is hidden otherwise.</summary>
    bool IsAvailable { get; }

    bool IsOn { get; set; }
}
