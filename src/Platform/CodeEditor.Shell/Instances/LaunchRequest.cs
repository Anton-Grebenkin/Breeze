namespace CodeEditor.Shell.Instances;

/// <summary>The command line: <c>Breeze.exe [--new-window] [folder | file]</c>.</summary>
/// <param name="Path">The full path of the folder or file; <c>null</c> without one.</param>
/// <param name="NewWindow">Open a window even if another one could take the request.</param>
public sealed record LaunchRequest(string? Path, bool NewWindow)
{
    public const string NewWindowFlag = "--new-window";

    private const string NewWindowShortFlag = "-n";

    public static LaunchRequest Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var newWindow = args.Any(IsNewWindowFlag);
        var path = args.FirstOrDefault(arg => !IsNewWindowFlag(arg) && !string.IsNullOrWhiteSpace(arg));
        return new LaunchRequest(path is null ? null : System.IO.Path.GetFullPath(path), newWindow);
    }

    private static bool IsNewWindowFlag(string arg) =>
        arg.Equals(NewWindowFlag, StringComparison.OrdinalIgnoreCase) || arg.Equals(NewWindowShortFlag, StringComparison.OrdinalIgnoreCase);
}
