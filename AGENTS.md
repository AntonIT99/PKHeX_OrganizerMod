# Organizer Mod contributor guidance

- Never modify anything under `external/PKHeX` unless the user explicitly requests it.
- Keep domain logic independent from PKHeX and Windows Forms.
- Calculate an organization plan before mutating any save data.
- Destructive operations must eventually provide a preview and explicit confirmation.
- Never overwrite occupied slots implicitly.
- Add unit tests for organization behavior.
- Run the relevant builds and tests before completing a task.
- Do not commit build output or generated packages.
- Do not package PKHeX assemblies with the plugin.
- Inspect the checked-out PKHeX API instead of assuming it is stable.

## Working commands

Run these from PowerShell:

```powershell
.\scripts\build-debug.ps1
.\scripts\run-debug.ps1
.\scripts\test.ps1
.\scripts\build-release.ps1
.\scripts\package-release.ps1
```

Direct solution verification:

```powershell
dotnet restore .\OrganizerMod.sln
dotnet build .\OrganizerMod.sln -c Debug
dotnet test .\OrganizerMod.sln -c Debug
```
