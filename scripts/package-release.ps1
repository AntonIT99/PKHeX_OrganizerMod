$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $repositoryRoot 'src\OrganizerMod\OrganizerMod.csproj'
$releaseOutput = Join-Path $repositoryRoot 'src\OrganizerMod\bin\Release\net10.0-windows'
$artifactsDirectory = Join-Path $repositoryRoot 'artifacts'
$installationReadme = Join-Path $repositoryRoot 'packaging\README.txt'

& (Join-Path $PSScriptRoot 'build-release.ps1')

$versionOutput = & dotnet msbuild $pluginProject -nologo -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw "Could not read the Organizer Mod version (exit code $LASTEXITCODE)."
}
$version = ($versionOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'The Organizer Mod project did not provide a Version property.'
}

$packageFiles = [ordered]@{
    'plugins/OrganizerMod/OrganizerMod.deps.json' = (Join-Path $releaseOutput 'OrganizerMod.deps.json')
    'plugins/OrganizerMod/OrganizerMod.Domain.dll' = (Join-Path $releaseOutput 'OrganizerMod.Domain.dll')
    'plugins/OrganizerMod/OrganizerMod.dll' = (Join-Path $releaseOutput 'OrganizerMod.dll')
    'plugins/OrganizerMod/README.txt' = $installationReadme
}

foreach ($sourcePath in $packageFiles.Values) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Expected release file is missing: '$sourcePath'."
    }
    if ((Split-Path -Leaf $sourcePath) -like 'PKHeX*.dll') {
        throw "Refusing to package a PKHeX assembly: '$sourcePath'."
    }
}

New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
$zipPath = Join-Path $artifactsDirectory "OrganizerMod-$version.zip"
$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDirectory)
$resolvedZip = [System.IO.Path]::GetFullPath($zipPath)
if (-not $resolvedZip.StartsWith($resolvedArtifacts + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write a package outside '$resolvedArtifacts'."
}
if (Test-Path -LiteralPath $resolvedZip) {
    Remove-Item -LiteralPath $resolvedZip
}

Add-Type -AssemblyName System.IO.Compression
$fileStream = [System.IO.File]::Open($resolvedZip, [System.IO.FileMode]::CreateNew)
try {
    $archive = [System.IO.Compression.ZipArchive]::new(
        $fileStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        $fixedTimestamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        foreach ($entryName in ($packageFiles.Keys | Sort-Object)) {
            $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp
            $entryStream = $entry.Open()
            try {
                $sourceStream = [System.IO.File]::OpenRead($packageFiles[$entryName])
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $fileStream.Dispose()
}

Write-Host "Created deterministic release package: $resolvedZip"
