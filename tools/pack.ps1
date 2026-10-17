[CmdletBinding()]
param(
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "VoidSniffer\VoidSniffer.csproj"
$outDir = Join-Path $root "dist"

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null |
            Select-Object -First 1
        if ($found) { return $found }
    }

    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw "MSBuild not found. Install Visual Studio with the '.NET desktop development' workload."
}

if (-not $NoBuild) {
    $msbuild = Find-MSBuild
    Write-Host "Building $Configuration ..."
    & $msbuild $project "/p:Configuration=$Configuration" /v:m /nologo | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
}

$bin = Join-Path $root "VoidSniffer\bin\$Configuration"
$exe = Join-Path $bin "VoidSniffer.exe"
if (-not (Test-Path $exe)) { throw "Not found: $exe. Build the project first." }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$zip = Join-Path $outDir ("VoidSniffer-{0}-win-anycpu.zip" -f $Version)
if (Test-Path $zip) { Remove-Item $zip -Force }

$items = @(
    $exe,
    (Join-Path $bin "VoidSniffer.exe.config"),
    (Join-Path $bin "VoidSniffer.ini"),
    (Join-Path $bin "lang")
) | Where-Object { Test-Path $_ }

Compress-Archive -Path $items -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Created: $zip"

# Only the archive path is written to the pipeline (used by release.ps1 / CI).
Write-Output $zip
