<#
.SYNOPSIS
    Prints the CHANGELOG.md section of a version: the release notes of the installer and the GitHub release.

.EXAMPLE
    ./build/release-notes.ps1 -Version 0.1.0-alpha.1 > notes.md
#>
param(
    [Parameter(Mandatory)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$changelog = Join-Path (Split-Path $PSScriptRoot -Parent) 'CHANGELOG.md'
$heading = "## [$Version]"

$lines = Get-Content $changelog -Encoding utf8
$start = [Array]::FindIndex($lines, [Predicate[string]] { param($line) $line.StartsWith($heading) })
if ($start -lt 0) {
    throw "CHANGELOG.md has no section '$heading': describe the release before tagging it."
}

$end = $start + 1
while ($end -lt $lines.Count -and -not $lines[$end].StartsWith('## [')) { $end++ }

# The section without its heading: the release title already names the version.
($lines[($start + 1)..($end - 1)] -join "`n").Trim()
