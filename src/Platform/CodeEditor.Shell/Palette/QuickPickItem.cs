namespace CodeEditor.Shell.Palette;

/// <summary>A quick pick option passed to the handler; the title is filtered, the detail is shown on the right.</summary>
public sealed record QuickPickItem(string Id, string Title)
{
    public string? Detail { get; init; }
}
