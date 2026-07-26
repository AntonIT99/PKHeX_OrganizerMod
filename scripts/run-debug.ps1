$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pkhexOutput = Join-Path $repositoryRoot 'external\PKHeX\PKHeX.WinForms\bin\Debug\net10.0-windows\win-x64'
$pkhexExecutable = Join-Path $pkhexOutput 'PKHeX.exe'

& (Join-Path $PSScriptRoot 'build-debug.ps1')

if (-not (Test-Path -LiteralPath $pkhexExecutable -PathType Leaf)) {
    throw "PKHeX executable was not found at '$pkhexExecutable'. Run build-debug.ps1 and inspect the build output."
}

Write-Host "Launching $pkhexExecutable"
Start-Process -FilePath $pkhexExecutable -WorkingDirectory $pkhexOutput
