namespace CodeEditor.Shell.Layout;

public interface ILayoutStore
{
    /// <summary>The saved layout, or <c>null</c> if there is none or the file is corrupt.</summary>
    LayoutState? Load();

    void Save(LayoutState state);
}
