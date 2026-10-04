using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// Which commands the agent runs without asking (ADR 0012): reads inside the workspace only, no writes, no invocation
/// bypassing the command name, no reading file contents (read_file and search_text do that and skip secrets).
/// </summary>
public sealed class ReadOnlyCommandsTests
{
    [Theory]
    [InlineData("git status")]
    [InlineData("git --no-pager log --oneline -n 20 -- src/App")]
    [InlineData("git diff HEAD~3..HEAD --stat")]
    [InlineData("git show HEAD:src/App/A.cs")]
    [InlineData("git log --format=%h:%s -n 5")]
    [InlineData("git branch -a")]
    [InlineData("git status 2>&1")]
    [InlineData("Get-ChildItem -Recurse -Filter *.cs | Select-Object -First 20")]
    [InlineData("gci src | Where-Object { $_.Length -gt 1000 } | Sort-Object Length")]
    [InlineData("Get-ChildItem src 2>$null")]
    [InlineData("dotnet --list-sdks")]
    [InlineData("rg --files src")]
    [InlineData("where.exe git")]
    public void ReadCommands_RunWithoutAsking(string command) =>
        Assert.True(ReadOnlyCommands.IsReadOnly(CommandTokenizer.Parse(command)), command);

    [Theory]
    [InlineData("Get-Content src/App/A.cs")]
    [InlineData("Select-String -Path *.cs -Pattern TODO")]
    [InlineData("rg -n TODO src")]
    [InlineData("Remove-Item a.txt")]
    [InlineData("git push")]
    [InlineData("git commit -m wip")]
    [InlineData("git branch feature")]
    [InlineData("git diff --output=patch.txt")]
    [InlineData("git status > out.txt")]
    [InlineData("$x = 1")]
    [InlineData("$x=1; git status")]
    [InlineData("(Get-Item a.txt).Delete()")]
    [InlineData("[IO.File]::ReadAllText('a.txt')")]
    [InlineData("& ./build.ps1")]
    [InlineData(". ./profile.ps1")]
    [InlineData("Get-ChildItem C:\\Windows")]
    [InlineData("Get-ChildItem ..\\other")]
    [InlineData("Get-ChildItem ~")]
    [InlineData("Get-ChildItem env:")]
    [InlineData("Get-ChildItem -Path:C:\\Users")]
    [InlineData("Get-ChildItem \"$(Remove-Item x)\"")]
    [InlineData("gci | ForEach-Object { Remove-Item $_ }")]
    [InlineData("Get-ChildItem .env")]
    [InlineData("git show HEAD:config/.env")]
    [InlineData("Get-ChildItem `\n -Recurse")]
    [InlineData("dotnet build")]
    [InlineData("Invoke-WebRequest https://example.com")]
    public void OtherCommands_AskTheUser(string command) =>
        Assert.False(ReadOnlyCommands.IsReadOnly(CommandTokenizer.Parse(command)), command);

    [Fact]
    public void Tokenizer_SplitsSegments_AndKeepsQuotedTextWhole()
    {
        var shape = CommandTokenizer.Parse("git log --grep 'a; b' -n 5; gci src | select -First 3");

        Assert.Equal(
            [["git", "log", "--grep", "a; b", "-n", "5"], ["gci", "src"], ["select", "-First", "3"]],
            shape.Segments.Select(segment => segment.ToArray()));
        Assert.True(shape.IsTransparent);
    }

    [Theory]
    [InlineData("cd src && dotnet build", "cd src; if ($?) { dotnet build }")]
    [InlineData("dotnet build || echo failed", "dotnet build; if (-not $?) { echo failed }")]
    [InlineData("a && b && c", "a; if ($?) { b; if ($?) { c } }")]
    [InlineData("echo 'a && b'", "echo 'a && b'")]
    [InlineData("gci | ? { $_ -and ($x -or $y) }", "gci | ? { $_ -and ($x -or $y) }")]
    public void Chains_AreRewrittenForPowerShell51(string command, string expected) =>
        Assert.Equal(expected, PowerShellChains.Rewrite(command));

    [Theory]
    [InlineData("rg TODO src", 1, true)]
    [InlineData("git diff --exit-code", 1, true)]
    [InlineData("robocopy a b /e", 3, true)]
    [InlineData("dotnet build", 1, false)]
    [InlineData("rg TODO src", 0, false)]
    public void ExitHints_ExplainNonErrors(string command, int exitCode, bool hasHint) =>
        Assert.Equal(hasHint, CommandExitHints.For(command, exitCode) is not null);
}
