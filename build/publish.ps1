<#
.SYNOPSIS
    Builds a Breeze release: dotnet publish (self-contained win-x64, ReadyToRun) and, with -Pack, the Velopack
    packages: Setup.exe, a portable zip and the full and delta update packages.

.DESCRIPTION
    The release workflow (.github/workflows/release.yml) runs it on a tag; locally it checks that the installer builds.
    Deltas need the previous release in the output folder: the workflow downloads it first (vpk download github).

.EXAMPLE
    ./build/publish.ps1 -Version 0.1.0-alpha.1 -Pack
#>
param(
    # SemVer without the "v" prefix of the tag.
    [Parameter(Mandatory)]
    [string] $Version,

    # The GitHub repository the app checks for updates; empty in local builds.
    [string] $RepositoryUrl = '',

    # Also build the installer and update packages with vpk.
    [switch] $Pack,

    [string] $PublishDir = 'publish',

    [string] $OutputDir = 'releases'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# Velopack installs to %LocalAppData%\<packId> and deletes that folder on uninstall, so the id must differ from
# %LocalAppData%\Breeze, where user settings and sessions live.
$packId = 'BreezeCodeEditor'
$authors = 'Антон Гребенкин'

Push-Location $root
try {
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

    dotnet publish src/Apps/CodeEditor.App/CodeEditor.App.csproj -c Release -r win-x64 --self-contained `
        -p:Version=$Version -p:RepositoryUrl=$RepositoryUrl -o $PublishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $Pack) { return }

    # Outside the output folder: vpk upload publishes everything it finds there.
    $notes = Join-Path ([IO.Path]::GetTempPath()) "breeze-notes-$Version.md"
    & (Join-Path $PSScriptRoot 'release-notes.ps1') -Version $Version | Set-Content $notes -Encoding utf8

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet vpk pack --packId $packId --packVersion $Version --packDir $PublishDir --mainExe Breeze.exe --runtime win-x64 `
        --packTitle Breeze --packAuthors $authors --icon assets/brand/breeze.ico --releaseNotes $notes `
        --outputDir $OutputDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
