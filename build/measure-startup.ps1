<#
.SYNOPSIS
    Measures how long a built Breeze takes to show its window.

.DESCRIPTION
    Starts the executable several times with its own empty data folder (CODEEDITOR_USER_DATA), so it neither reads the
    user's settings nor meets a running Breeze, waits for the first frame, reads the phases the app writes
    (CODEEDITOR_STARTUP_TRACE) and stops the process it started. The first run is the cold one. Prints every run and
    fails if the best run is over the budget.

    The window opens on screen and may take the keyboard focus: do not type while it runs.

.EXAMPLE
    ./build/measure-startup.ps1 -Exe publish/Breeze.exe

.EXAMPLE
    ./build/measure-startup.ps1 -Exe publish/Breeze.exe -BudgetMs 4000 -Runs 2
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Exe,

    # The budget from CONTRIBUTING.md is 1 s on a developer machine; a CI runner is several times slower.
    [int] $BudgetMs = 1000,

    [int] $Runs = 3,

    [int] $TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path $Exe).Path
$work = Join-Path ([IO.Path]::GetTempPath()) ("breeze-startup-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$results = @()

try {
    for ($run = 1; $run -le $Runs; $run++) {
        $trace = Join-Path $work "trace-$run.txt"
        $env:CODEEDITOR_USER_DATA = Join-Path $work "data-$run"
        $env:CODEEDITOR_STARTUP_TRACE = $trace
        $process = Start-Process -FilePath $exePath -PassThru
        try {
            $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
            while (-not (Test-Path $trace) -and -not $process.HasExited -and (Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 100
            }

            if (-not (Test-Path $trace)) {
                throw "Run ${run}: no startup trace in $TimeoutSeconds s (the window did not render, or the process exited with code $($process.ExitCode))."
            }

            # The app writes the file in one call, right after the first frame.
            Start-Sleep -Milliseconds 200
            $phases = [ordered]@{}
            foreach ($line in Get-Content $trace) {
                $name, $value = $line -split "`t"
                $phases[$name] = [int] $value
            }

            $results += [pscustomobject]@{ Run = $run; TotalMs = $phases['process-to-render']; Phases = $phases }
        }
        finally {
            # Only the process this script started.
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit(10000) | Out-Null
            }
        }
    }
}
finally {
    Remove-Item Env:CODEEDITOR_USER_DATA, Env:CODEEDITOR_STARTUP_TRACE -ErrorAction SilentlyContinue
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

$lines = @("Startup of $exePath, process start to first frame:")
foreach ($result in $results) {
    $detail = ($result.Phases.GetEnumerator() | Where-Object Key -ne 'process-to-render' | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '; '
    $lines += "  run $($result.Run): $($result.TotalMs) ms ($detail)"
}

$best = ($results | Measure-Object -Property TotalMs -Minimum).Minimum
$lines += "Best: $best ms, budget: $BudgetMs ms."
$lines | ForEach-Object { Write-Host $_ }

# In GitHub Actions the numbers go to the job summary, so every release shows them.
if ($env:GITHUB_STEP_SUMMARY) {
    "### Startup`n" + (($lines | ForEach-Object { "- $_" }) -join "`n") | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

if ($best -gt $BudgetMs) {
    Write-Error "Startup takes $best ms, over the budget of $BudgetMs ms."
    exit 1
}
