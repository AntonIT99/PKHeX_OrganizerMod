$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $repositoryRoot 'src\OrganizerMod\OrganizerMod.csproj'
$domainProject = Join-Path $repositoryRoot 'src\OrganizerMod.Domain\OrganizerMod.Domain.csproj'
$pkhexCoreProject = Join-Path $repositoryRoot 'external\PKHeX\PKHeX.Core\PKHeX.Core.csproj'
$pkhexCoreAssembly = Join-Path $repositoryRoot 'external\PKHeX\PKHeX.Core\bin\Release\net10.0\PKHeX.Core.dll'

Write-Host 'Building Organizer Mod (Release)...'
if (-not (Test-Path -LiteralPath $pkhexCoreAssembly -PathType Leaf)) {
    Write-Host 'PKHeX.Core Release reference output is missing; building it once...'
    & dotnet build $pkhexCoreProject --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "PKHeX.Core Release reference build failed with exit code $LASTEXITCODE."
    }
}

& dotnet build $domainProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Organizer Mod Domain Release build failed with exit code $LASTEXITCODE."
}

# Avoid rebuilding PKHeX.Core on every plugin build. PKHeX generates a timestamped
# source revision, so unnecessarily rebuilding it would also make plugin binaries
# (and therefore otherwise deterministic ZIPs) change on every invocation.
& dotnet build $pluginProject --configuration Release --no-dependencies
if ($LASTEXITCODE -ne 0) {
    throw "Organizer Mod Release build failed with exit code $LASTEXITCODE."
}
