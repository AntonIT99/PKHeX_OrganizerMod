$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot 'tests\OrganizerMod.Tests\OrganizerMod.Tests.csproj'

& dotnet test $testProject --configuration Debug
if ($LASTEXITCODE -ne 0) {
    throw "Organizer Mod tests failed with exit code $LASTEXITCODE."
}
