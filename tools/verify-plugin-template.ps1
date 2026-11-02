# Verifies the plugin template (plan M12 step 6): packs every RIoT2.Core package into a local feed, installs
# RIoT2.Core.Devices.Templates into a private template hive, runs "dotnet new riot2-plugin", and checks that the
# generated plugin builds without warnings (CI settings), passes its tests (PluginConformance included) and that
# "dotnet publish" writes the node's plugin zip with its manifest and sidecar. Loading the zip into the node is
# RIoT2.Net.Node's TemplatePluginInstallsAndLoadsIntoNode.
#
#   pwsh tools/verify-plugin-template.ps1            # CI (Core's ci.yml), or Windows PowerShell 5.1
#   pwsh tools/verify-plugin-template.ps1 -Feed C:\Src\RIoT2\.localfeed   # reuse packed packages
param(
    [string]$Feed = "",
    [string]$Work = (Join-Path ([System.IO.Path]::GetTempPath()) ("riot2-template-" + [Guid]::NewGuid().ToString("N")))
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Force $Work | Out-Null
$props = [xml](Get-Content (Join-Path $root "Directory.Build.repo.props") -Raw)
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $Feed) {
    $Feed = Join-Path $Work "feed"
    Invoke-Dotnet pack (Join-Path $root "RIoT2.Core.sln") -c Release -o $Feed --nologo
}
$template = Join-Path $Feed "RIoT2.Core.Devices.Templates.$version.nupkg"
if (-not (Test-Path $template)) { throw "$template is missing; pack RIoT2.Core.sln first." }

$hive = Join-Path $Work "hive"
$plugin = Join-Path $Work "Acme.Smoke"
Invoke-Dotnet new install $template --debug:custom-hive $hive
Invoke-Dotnet new riot2-plugin -n Acme.Smoke -o $plugin --DisplayName "Smoke devices" --debug:custom-hive $hive

$packages = Get-Content (Join-Path $plugin "Directory.Packages.props") -Raw
if ($packages -notmatch [regex]::Escape("Version=`"$version`"")) { throw "Directory.Packages.props doesn't use Core $version." }
foreach ($file in @("README.md", "AGENTS.md", "CHANGELOG.md", "CLAUDE.md", ".editorconfig", "Directory.Build.props", ".github/workflows/ci.yml", ".github/workflows/release.yml", "Tests/PluginTests.cs")) {
    if (-not (Test-Path (Join-Path $plugin $file))) { throw "The generated plugin has no $file." }
}

$sources = "-p:RestoreAdditionalProjectSources=$Feed"
Invoke-Dotnet test (Join-Path $plugin "Acme.Smoke.sln") -c Release -p:CI=true $sources --nologo
Invoke-Dotnet publish (Join-Path $plugin "Acme.Smoke.csproj") -c Release -p:CI=true $sources --nologo

$zip = Get-ChildItem (Join-Path $plugin "bin/plugin") -Filter "Acme.Smoke_*.zip" | Select-Object -First 1
if (-not $zip) { throw "dotnet publish wrote no plugin zip." }
if (-not (Test-Path ($zip.FullName + ".sha256"))) { throw "The zip has no .sha256 sidecar." }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip.FullName)
try {
    $names = $archive.Entries | ForEach-Object { $_.FullName }
    if ($names -notcontains "Acme.Smoke.dll") { throw "The zip has no Acme.Smoke.dll." }
    if ($names | Where-Object { $_ -like "RIoT2.Core*.dll" }) { throw "The zip ships RIoT2.Core assemblies; the node provides them." }
    $reader = New-Object System.IO.StreamReader(($archive.Entries | Where-Object { $_.FullName -eq "PluginManifest.json" }).Open())
    $manifest = $reader.ReadToEnd() | ConvertFrom-Json
    $reader.Dispose()
} finally { $archive.Dispose() }
if ($manifest.name -ne "Smoke devices" -or $manifest.coreVersion -ne $version) {
    throw "PluginManifest.json is $($manifest | ConvertTo-Json -Compress); expected name 'Smoke devices' and coreVersion $version."
}
Write-Host "The plugin template works: $($zip.Name), Core $version, files: $($names -join ', ')"
Remove-Item -Recurse -Force $Work -ErrorAction SilentlyContinue
