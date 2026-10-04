namespace CodeEditor.Modules.Terminal.Services.Shells;

/// <summary>A shell a terminal can start.</summary>
/// <param name="Id">The <c>terminal.defaultProfile</c> value: <c>pwsh</c>, <c>powershell</c>, <c>cmd</c>, <c>gitbash</c>.</param>
public sealed record TerminalProfile(string Id, string Name, string Executable, string Arguments = "")
{
    public string CommandLine => Arguments.Length == 0 ? $"\"{Executable}\"" : $"\"{Executable}\" {Arguments}";
}
