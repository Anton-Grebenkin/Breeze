namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Environment for <c>dotnet</c> runs: English CLI and MSBuild messages, so parsing errors and test totals doesn't
/// depend on the system language; no SDK banner.
/// </summary>
public static class DotNetEnvironment
{
    public const string Executable = "dotnet";

    public static IReadOnlyDictionary<string, string> Variables { get; } = new Dictionary<string, string>
    {
        ["DOTNET_CLI_UI_LANGUAGE"] = "en",
        ["VSLANG"] = "1033",
        ["DOTNET_NOLOGO"] = "1",
    };
}
