namespace CodeEditor.Shell.Instances;

/// <summary>What Windows shows for Breeze, in the app's language.</summary>
/// <param name="OpenIn">The context menu item, e.g. "Open in Breeze".</param>
/// <param name="FileType">The name of a file type with the extension as <c>{0}</c>, e.g. "{0} File".</param>
/// <param name="Description">The app's description in "Default apps".</param>
public sealed record ExplorerTexts(string OpenIn, string FileType, string Description);
