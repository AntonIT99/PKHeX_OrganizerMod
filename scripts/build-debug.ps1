$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pkhexProject = Join-Path $repositoryRoot 'external\PKHeX\PKHeX.WinForms\PKHeX.WinForms.csproj'
$pluginProject = Join-Path $repositoryRoot 'src\OrganizerMod\OrganizerMod.csproj'

Write-Host 'Building PKHeX WinForms (Debug)...'
& dotnet build $pkhexProject --configuration Debug
if ($LASTEXITCODE -ne 0) {
    throw "PKHeX Debug build failed with exit code $LASTEXITCODE."
}

Write-Host 'Building Organizer Mod (Debug) and copying it to PKHeX...'
& dotnet build $pluginProject --configuration Debug
if ($LASTEXITCODE -ne 0) {
    throw "Organizer Mod Debug build failed with exit code $LASTEXITCODE."
}
