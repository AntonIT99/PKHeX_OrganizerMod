# Organizer Mod

Organizer Mod is an early-development PKHeX Windows Forms plugin intended to plan and preview safe Pokémon storage organization. The current implementation is deliberately small: it proves plugin loading, adds **Tools > Organizer Mod**, displays basic information about the active save, and provides a UI-independent domain model with tests. It does not sort, move, or mutate Pokémon yet.

## Prerequisites

- Windows
- .NET SDK 10.0.301 or a compatible 10.0 feature-band patch selected by `global.json`
- Git with submodule support
- PKHeX's normal Windows build prerequisites
- Optional: Visual Studio, Rider, or VS Code with the C# debugger

The current Organizer Mod sources target `net10.0-windows`. The domain and tests target `net10.0`.

## Clone and restore the submodule

For a fresh clone:

```powershell
git clone --recurse-submodules <repository-url>
cd PKHeX_OrganizerMod
```

If the repository was cloned normally, initialize the pinned PKHeX checkout afterward:

```powershell
git submodule update --init --recursive
```

PKHeX is an external upstream dependency located at `external/PKHeX`. Do not edit files in that directory as part of Organizer Mod development.

## Build

Build PKHeX Debug, then Organizer Mod Debug:

```powershell
.\scripts\build-debug.ps1
```

The Organizer Mod post-build target creates:

```text
external/PKHeX/PKHeX.WinForms/bin/Debug/net10.0-windows/win-x64/plugins/OrganizerMod/
```

It copies the plugin DLL, domain DLL, dependency manifest, and Debug PDB files. It does not copy `PKHeX.Core.dll` or `PKHeX.dll`.

Direct solution commands are also available:

```powershell
dotnet restore .\OrganizerMod.sln
dotnet build .\OrganizerMod.sln -c Debug
```

The solution contains only Organizer Mod projects. Build PKHeX itself with `build-debug.ps1` or its WinForms project.

## Test

Run only Organizer Mod's UI-independent tests:

```powershell
.\scripts\test.ps1
```

Equivalent direct command:

```powershell
dotnet test .\OrganizerMod.sln -c Debug
```

## Run and debug

To build and launch PKHeX:

```powershell
.\scripts\run-debug.ps1
```

`run-debug.ps1` starts `PKHeX.exe` with its Debug output directory as the working directory, which is also the base for PKHeX's default relative `plugins` path.

For VS Code, first run `build-debug.ps1`, set a breakpoint in `OrganizerPlugin.Initialize`, and use the committed **Debug PKHeX with Organizer Mod** launch configuration. In Visual Studio or Rider, create an executable launch configuration with:

```text
Executable: external/PKHeX/PKHeX.WinForms/bin/Debug/net10.0-windows/win-x64/PKHeX.exe
Working directory: external/PKHeX/PKHeX.WinForms/bin/Debug/net10.0-windows/win-x64
```

Use repository-relative paths or the IDE's repository-root macro rather than committing a machine-specific path.

## Release and packaging

Build only plugin-owned Release output:

```powershell
.\scripts\build-release.ps1
```

Create a deterministic ZIP under the ignored `artifacts` directory:

```powershell
.\scripts\package-release.ps1
```

The ZIP preserves `plugins/OrganizerMod/`. To install it, close PKHeX and extract the archive into the directory containing `PKHeX.exe`. The package excludes PDBs, test assemblies, PKHeX, and PKHeX.Core.

## Safety

Organizer Mod is experimental. Test only with copied save files and keep known-good backups. Planned organization behavior must calculate and preview moves before any future save mutation, must request confirmation for destructive operations, and must never overwrite occupied slots implicitly.

## PKHeX compatibility

This setup was inspected and built against PKHeX commit `b483ad47f89f859caa726f09cea283e6a2a13809` (`26.07.07-18-gb483ad47f`). PKHeX's plugin API is not assumed to be stable; inspect it and retest whenever the submodule changes.

Update PKHeX deliberately:

```powershell
git -C .\external\PKHeX fetch
git -C .\external\PKHeX checkout <reviewed-commit-or-tag>
git add .\external\PKHeX
```

Review upstream changes to `IPlugin`, `ISaveFileProvider`, plugin loading, frameworks, and output paths before committing the new submodule pointer.

## Licensing

PKHeX has its own license in `external/PKHeX/LICENSE`. Organizer Mod contributors and distributors should review that license and the licenses of any future dependencies before distribution. This repository should state its own licensing terms separately; no additional legal conclusion is asserted here.
