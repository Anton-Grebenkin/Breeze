namespace CodeEditor.Shell.ToolWindows;

public enum ToolWindowLocation
{
    /// <summary>Left side bar, switched by activity bar icons.</summary>
    SideBar,

    /// <summary>Bottom panel with tabs: output, problems, terminal.</summary>
    Panel,

    /// <summary>Secondary side bar on the right, as in VS Code; hosts the agent chat.</summary>
    SecondarySideBar,

    /// <summary>The editor area: the tool window is a tab next to files (ADR 0031).</summary>
    Editor,
}
