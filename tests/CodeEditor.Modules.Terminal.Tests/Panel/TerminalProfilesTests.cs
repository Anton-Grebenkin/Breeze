using CodeEditor.Modules.Terminal.Services.Shells;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Terminal.Tests.Panel;

public sealed class TerminalProfilesTests
{
    private const string System32 = @"C:\Windows\System32";
    private const string ProgramFiles = @"C:\Program Files";

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddFile(System32 + @"\cmd.exe", string.Empty)
        .AddFile(System32 + @"\WindowsPowerShell\v1.0\powershell.exe", string.Empty);

    [Fact]
    public void AllInstalled_InOrderOfPreference()
    {
        _fileSystem
            .AddFile(@"C:\Tools\pwsh.exe", string.Empty)
            .AddFile(ProgramFiles + @"\Git\bin\bash.exe", string.Empty);

        var profiles = Profiles(path: @"C:\Tools").Available();

        Assert.Equal([TerminalProfiles.PowerShell, TerminalProfiles.WindowsPowerShell, TerminalProfiles.CommandPrompt, TerminalProfiles.GitBash], profiles.Select(profile => profile.Id));
        Assert.Equal(@"""C:\Tools\pwsh.exe"" -NoLogo", profiles[0].CommandLine);
        Assert.Equal(@"""C:\Program Files\Git\bin\bash.exe"" --login -i", profiles[3].CommandLine);
    }

    [Fact]
    public void PowerShell7_FoundInProgramFilesWithoutPath()
    {
        _fileSystem.AddFile(ProgramFiles + @"\PowerShell\7\pwsh.exe", string.Empty);

        Assert.Equal(ProgramFiles + @"\PowerShell\7\pwsh.exe", Profiles().Default(null)?.Executable);
    }

    [Fact]
    public void Default_PrefersTheSetting_ElseTheFirstInstalled()
    {
        var profiles = Profiles();

        Assert.Equal(TerminalProfiles.CommandPrompt, profiles.Default("CMD")?.Id);
        Assert.Equal(TerminalProfiles.WindowsPowerShell, profiles.Default(TerminalProfiles.GitBash)?.Id);
    }

    [Fact]
    public void CommandInterpreter_ComesFromComSpec()
    {
        _fileSystem.AddFile(@"D:\shell\cmd.exe", string.Empty);

        var profiles = new TerminalProfiles(_fileSystem, new ShellLocations(System32, ProgramFiles, Path: string.Empty, CommandInterpreter: @"D:\shell\cmd.exe"));

        Assert.Equal(@"D:\shell\cmd.exe", profiles.Default(TerminalProfiles.CommandPrompt)?.Executable);
    }

    private TerminalProfiles Profiles(string path = "") =>
        new(_fileSystem, new ShellLocations(System32, ProgramFiles, path, CommandInterpreter: null));
}
