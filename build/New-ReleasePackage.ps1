<#
.SYNOPSIS
    Tests, builds and zips a release of the plugin.

.DESCRIPTION
    Runs the unit tests, builds the plugin in Release with the given version, and writes
    artifacts/DevDogs.WebDevices-<version>.zip containing the two plugin assemblies (with symbols),
    install notes, README and CHANGELOG. SimHub's own assemblies are never included; the script fails
    if anything else ends up in the package.

.PARAMETER Version
    Semantic version, for example 0.1.0 or 0.2.0-dev.1.

.PARAMETER SimHubPath
    SimHub install folder used to resolve SimHub's assemblies at build time. Defaults to the folder SimHub
    records in the registry.

.PARAMETER SkipTests
    Skip the unit tests (not for real releases).

.EXAMPLE
    ./build/New-ReleasePackage.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-dev\.\d+)?$')]
    [string]$Version,

    [string]$SimHubPath,

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $SimHubPath) {
    $SimHubPath = (Get-ItemProperty -Path 'HKCU:\Software\SimHub' -Name InstallDirectory -ErrorAction SilentlyContinue).InstallDirectory
}
if (-not $SimHubPath) {
    $SimHubPath = 'C:\Program Files (x86)\SimHub'
}

if (-not $SkipTests) {
    dotnet test (Join-Path $repoRoot 'tests\DevDogs.WebDevices.Core.Tests') -c Release -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed (exit code $LASTEXITCODE)."
    }
}

$project = Join-Path $repoRoot 'src\DevDogs.WebDevices\DevDogs.WebDevices.csproj'
dotnet build $project -c Release -nologo "-p:Version=$Version" "-p:SimHubPath=$SimHubPath"
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)."
}

$output = Join-Path $repoRoot 'src\DevDogs.WebDevices\bin\Release\net48'
$artifacts = Join-Path $repoRoot 'artifacts'
$staging = Join-Path $artifacts "staging\$Version"
$zip = Join-Path $artifacts "DevDogs.WebDevices-$Version.zip"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Force $staging | Out-Null

$assemblies = 'DevDogs.WebDevices.dll', 'DevDogs.WebDevices.pdb', 'DevDogs.WebDevices.Core.dll', 'DevDogs.WebDevices.Core.pdb'
foreach ($file in $assemblies) {
    Copy-Item (Join-Path $output $file) $staging
}
Copy-Item (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'CHANGELOG.md') $staging

@"
DevDogs Web Devices $Version
============================

1. Close SimHub.
2. Copy DevDogs.WebDevices.dll and DevDogs.WebDevices.Core.dll (and the .pdb files, optional) into the
   SimHub folder, usually C:\Program Files (x86)\SimHub.
3. Start SimHub and enable "DevDogs Web Devices" when asked.
4. Open "Web Devices" in SimHub's left menu, add a device, pick dashboards, click Save.
5. On the device, open the URL shown under "Open on device".

See README.md for details and troubleshooting.
"@ | Set-Content -Path (Join-Path $staging 'INSTALL.txt') -Encoding UTF8

# Guard against shipping SimHub's or Newtonsoft's assemblies by accident.
$expected = @($assemblies) + 'README.md', 'CHANGELOG.md', 'INSTALL.txt' | Sort-Object
$actual = Get-ChildItem $staging -File | ForEach-Object Name | Sort-Object
if (Compare-Object $expected $actual) {
    throw "Unexpected package contents: $($actual -join ', ')"
}

$builtVersion = (Get-Item (Join-Path $staging 'DevDogs.WebDevices.dll')).VersionInfo.ProductVersion
if (-not $builtVersion.StartsWith($Version)) {
    throw "Built assembly reports version '$builtVersion', expected '$Version'."
}

if (Test-Path $zip) {
    Remove-Item $zip -Force
}
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Package: $zip"
Write-Host "SHA256:  $hash"
