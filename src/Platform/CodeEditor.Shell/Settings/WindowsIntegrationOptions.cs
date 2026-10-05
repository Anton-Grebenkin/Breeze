namespace CodeEditor.Shell.Settings;

/// <summary>
/// The <c>windowsIntegration</c> settings section: what an installed Breeze adds to Windows Explorer (ADR 0046).
/// </summary>
public sealed class WindowsIntegrationOptions
{
    public const string Section = "windowsIntegration";
    public const string ContextMenuKey = Section + ".contextMenu";
    public const string FileTypesKey = Section + ".fileTypes";

    /// <summary>"Open in Breeze" in the Explorer context menu of files and folders.</summary>
    public bool ContextMenu { get; set; } = true;

    /// <summary>
    /// Breeze as an app for code files. <c>null</c> until the user answers: Windows gives the Breeze icon to files
    /// without an app of their own, so it is never turned on without asking.
    /// </summary>
    public bool? FileTypes { get; set; }
}
