[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Notes = "",
    [switch]$Draft
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^v') { $Version = "v$Version" }
if ($Version -notmatch '^v\d+\.\d+(\.\d+)?') {
    throw "Version must look like v1.2.3 (got: $Version)."
}

$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) not found: https://cli.github.com/"
}

$dirty = & git -C $root status --porcelain
if ($dirty) {
    Write-Warning "The working tree has uncommitted changes - the release will be built from the current files."
}

$zip = & (Join-Path $PSScriptRoot "pack.ps1") -Version $Version
if (-not $zip) { throw "Packaging failed." }

$existingTag = & git -C $root tag --list $Version
if (-not $existingTag) {
    & git -C $root tag -a $Version -m "VoidSniffer $Version"
}
& git -C $root push origin $Version

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $Notes = "VoidSniffer $Version`n`nRequirements: Windows 7+, .NET Framework 4.8, Everything 1.4+ running.`nEnable folder/file sizes (and preferably dates) in the Everything index."
}

$ghArgs = @("release", "create", $Version, $zip,
    "--title", "VoidSniffer $Version",
    "--notes", $Notes)
if ($Draft) { $ghArgs += "--draft" }

& gh @ghArgs
if ($LASTEXITCODE -ne 0) { throw "gh release create failed ($LASTEXITCODE)." }

Write-Host "Release $Version published."
