<#
.SYNOPSIS
    Builds the plugin and copies it into the local SimHub install for testing.

.DESCRIPTION
    SimHub locks plugin DLLs while it runs, so SimHub must be closed first. Writing into
    "Program Files (x86)" may need an elevated shell, depending on the folder's permissions.

.PARAMETER Configuration
    Build configuration. Defaults to Debug so symbols match the copied PDBs.

.PARAMETER SimHubPath
    SimHub install folder. Defaults to the folder SimHub records in the registry.

.EXAMPLE
    ./build/Install-DevBuild.ps1
    ./build/Install-DevBuild.ps1 -Configuration Release -SimHubPath 'D:\SimHub'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$SimHubPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $SimHubPath) {
    $SimHubPath = (Get-ItemProperty -Path 'HKCU:\Software\SimHub' -Name InstallDirectory -ErrorAction SilentlyContinue).InstallDirectory
}
if (-not $SimHubPath) {
    $SimHubPath = 'C:\Program Files (x86)\SimHub'
}
if (-not (Test-Path (Join-Path $SimHubPath 'SimHub.Plugins.dll'))) {
    throw "SimHub not found at '$SimHubPath'. Pass -SimHubPath <folder>."
}

if (Get-Process -Name 'SimHubWPF' -ErrorAction SilentlyContinue) {
    throw 'SimHub is running and locks the plugin DLLs. Close SimHub, then run this script again.'
}

$project = Join-Path $repoRoot 'src\DevDogs.WebDevices\DevDogs.WebDevices.csproj'
dotnet build $project -c $Configuration -nologo "-p:SimHubPath=$SimHubPath"
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)."
}

$output = Join-Path $repoRoot "src\DevDogs.WebDevices\bin\$Configuration\net48"
$files = 'DevDogs.WebDevices.dll', 'DevDogs.WebDevices.pdb', 'DevDogs.WebDevices.Core.dll', 'DevDogs.WebDevices.Core.pdb'

foreach ($file in $files) {
    $source = Join-Path $output $file
    try {
        Copy-Item -Path $source -Destination $SimHubPath -Force
    }
    catch [System.UnauthorizedAccessException] {
        throw "No write access to '$SimHubPath'. Run this script from an elevated PowerShell."
    }
    Write-Host "Copied $file"
}

Write-Host "Installed $Configuration build into '$SimHubPath'. Start SimHub and enable 'DevDogs Web Devices' if prompted."
