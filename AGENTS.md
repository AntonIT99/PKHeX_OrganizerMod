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
- Keep type allocation identifiers stable and language-neutral in the domain; localization belongs in the PKHeX adapter.
- Keep allocation search deterministic and bounded, and represent box-name changes in the previewable plan.
- Keep Living Dex entry keys language-neutral; PKHeX adapters own species/form definitions and display names.
- Represent excluded Pokémon as fixed preserved slots in the plan, and route all organization strategies through the shared validated snapshot/rollback applier.
- Keep duplicate-removal ranking lexicographic and deterministic; removal plans clear only explicitly previewed slots and must never compact boxes as a side effect.
- Keep PKM database import conflict resolution in the domain with PID precedence, and require selected-slot plus source-file stale validation before applying imports or replacements.

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
