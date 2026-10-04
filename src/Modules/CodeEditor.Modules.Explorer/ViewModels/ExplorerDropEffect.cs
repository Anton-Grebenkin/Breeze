namespace CodeEditor.Modules.Explorer.ViewModels;

/// <summary>What dropping files onto the explorer tree does.</summary>
public enum ExplorerDropEffect
{
    /// <summary>The drop is refused: into the folder itself, its descendant, or where the items already are.</summary>
    None,

    Move,

    Copy,
}
