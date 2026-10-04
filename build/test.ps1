<#
.SYNOPSIS
    Runs the Breeze tests: every tests/*.Tests project. UI tests (FlaUI) start the real window and move the mouse,
    so they run only with -Ui.

.EXAMPLE
    ./build/test.ps1
    ./build/test.ps1 -Configuration Release -NoBuild
    ./build/test.ps1 -Ui
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    # Also run CodeEditor.UI.Tests; do not touch the mouse and keyboard while they run.
    [switch] $Ui,

    # The solution is already built in this configuration (CI builds it in a separate step).
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$uiTests = 'CodeEditor.UI.Tests'

if (-not $NoBuild) {
    dotnet build (Join-Path $root 'CodeEditor.slnx') -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$projects = Get-ChildItem (Join-Path $root 'tests') -Directory -Filter '*.Tests' |
    Where-Object { $Ui -or $_.Name -ne $uiTests } |
    Sort-Object { $_.Name -eq $uiTests }, Name

# On GitHub Actions each failed test becomes an annotation: the run page and the API show it without access to the log.
function Write-FailureAnnotations([string] $project, [string[]] $output) {
    $contextLines = 6
    for ($i = 0; $i -lt $output.Count; $i++) {
        if ($output[$i] -notmatch '^(failed|сбой) ') { continue }
        $last = [Math]::Min($i + $contextLines, $output.Count - 1)
        $message = ($output[$i..$last] -join "`n").Replace('%', '%25').Replace("`r", '').Replace("`n", '%0A')
        Write-Host "::error title=$project::$message"
    }
}

$failed = @()
foreach ($project in $projects) {
    Write-Host "`n=== $($project.Name) ===" -ForegroundColor Cyan
    dotnet test --project $project.FullName -c $Configuration --no-build | Tee-Object -Variable output | Out-Host
    if ($LASTEXITCODE -eq 0) { continue }

    $failed += $project.Name
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-FailureAnnotations $project.Name $output }
}

if ($failed.Count -gt 0) {
    Write-Host "`nFailed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`nAll $($projects.Count) test projects passed." -ForegroundColor Green
