using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// Which commands the agent runs without asking (ADR 0012): reads inside the workspace only, no writes, no invocation
/// bypassing the command name; file contents only from files named outright, so a wildcard or a file list can't pull
/// in a secret that read_file would refuse; network only to this machine.
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
    [InlineData("git worktree list")]
    [InlineData("git rev-list --count HEAD")]
    [InlineData("Get-ChildItem -Recurse -Filter *.cs | Select-Object -First 20")]
    [InlineData("gci src | Where-Object { $_.Length -gt 1000 } | Sort-Object Length")]
    [InlineData("Get-ChildItem src 2>$null")]
    [InlineData("dotnet --list-sdks")]
    [InlineData("dotnet list src/App/App.csproj package")]
    [InlineData("rg --files src")]
    [InlineData("where.exe git")]
    [InlineData("Get-Content src/App/A.cs -TotalCount 50")]
    [InlineData("Get-Content -Path:src/App/A.cs -Raw")]
    [InlineData("Select-String -Path src/App/A.cs -Pattern \"TODO|FIXME\"")]
    [InlineData("git log -n 50 | Select-String fix")]
    [InlineData("cd src; Get-ChildItem")]
    [InlineData("$x = 1")]
    [InlineData("$x=1; git status")]
    [InlineData("$files = Get-ChildItem src; $files.Count")]
    [InlineData("$port = 5432")]
    [InlineData("docker ps --format \"{{.Names}} {{.Status}}\"")]
    [InlineData("docker compose logs --tail 50 api")]
    [InlineData("docker logs app-db-1 --tail 20")]
    [InlineData("Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue")]
    [InlineData("Get-NetTCPConnection -LocalPort 5000,5001 -ErrorAction SilentlyContinue")]
    [InlineData("Get-Process -Id 8020")]
    [InlineData("Invoke-WebRequest -Uri http://localhost:5000/health -UseBasicParsing -TimeoutSec 10")]
    [InlineData("irm http://127.0.0.1:5000/api/items -Method Get")]
    [InlineData("node --version")]
    public void ReadCommands_RunWithoutAsking(string command) =>
        Assert.True(ReadOnlyCommands.IsReadOnly(CommandTokenizer.Parse(command)), command);

    [Theory]
    [InlineData("Select-String -Path *.cs -Pattern TODO")]
    [InlineData("Get-Content *.env")]
    [InlineData("Get-Content .env")]
    [InlineData("Get-Content $path")]
    [InlineData("Get-Content -Include *.cs -Path src")]
    [InlineData("Get-ChildItem -Recurse | Select-String password")]
    [InlineData("$files = gci -Recurse; $files | sls key")]
    [InlineData("Get-ChildItem *.cs | Get-Content")]
    [InlineData("Select-String *.env -Pattern key")]
    [InlineData("rg -n TODO src")]
    [InlineData("Remove-Item a.txt")]
    [InlineData("git push")]
    [InlineData("git commit -m wip")]
    [InlineData("git branch feature")]
    [InlineData("git diff --output=patch.txt")]
    [InlineData("git status > out.txt")]
    [InlineData("git reflog expire --all")]
    [InlineData("$x = Remove-Item a.txt")]
    [InlineData("$x += Remove-Item a.txt")]
    [InlineData("$x+=1")]
    [InlineData("$env:PATH = 'x'")]
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
    [InlineData("cd ..")]
    [InlineData("dotnet build")]
    [InlineData("docker rm api")]
    [InlineData("docker compose config")]
    [InlineData("docker compose up -d")]
    [InlineData("Invoke-WebRequest https://example.com")]
    [InlineData("Invoke-WebRequest -Uri:https://example.com")]
    [InlineData("iwr http://localhost:5000/api -Method Post -Body x")]
    [InlineData("curl -o out.html http://localhost:5000")]
    [InlineData("Test-NetConnection example.com -Port 443")]
    [InlineData("tnc -ComputerName:example.com")]
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
