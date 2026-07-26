# Organizer Mod

Organizer Mod is an early-development PKHeX Windows Forms plugin intended to plan and preview safe Pokémon storage organization. It adds **Tools > Organizer Mod**, displays basic information about the active save, and provides a UI-independent domain model with tests.

## Type-Optimized Box Allocation

Open **Tools > Organizer Mod > Type-Optimized Box Allocation** (or open the Organizer window) to organize selected storage boxes by shared Pokémon type. Party Pokémon, pension Pokémon, empty slots, unselected boxes, and unavailable boxes are not part of this operation. A box is unavailable when PKHeX reports a locked, reserved, battle-team, or party-backed slot, or when it contains unsupported type data. The current implementation supports save formats with 30-slot boxes.

The planner treats dual-type assignment as a global allocation problem. For small ambiguous sets it evaluates every assignment within a fixed 16-Pokémon limit. Larger sets use a deterministic complete assignment followed by bounded single- and paired-reassignment improvement. Higher-priority layout criteria are compared lexicographically, so a lower-priority improvement cannot outweigh a lost full type box.

- **Compact** maximizes full coherent boxes first. A partial type group is retained when it is at least half full and does not increase the number of boxes required for the residual layout; smaller inefficient groups are packed into `Mixed` overflow boxes. A lone represented type remains a coherent type box even when partial.
- **Expanded by Type** keeps a separate adjacent box group for every represented type when the selected boxes permit it. If total slot capacity is sufficient but there are too few boxes for every partial group, the preview clearly warns that the remaining residual groups use best-effort mixed overflow. Nothing is applied unless the user accepts that preview.

The **Boxes to organize** checklist is the feature's explicit source, destination, and preservation boundary: Pokémon currently in selected boxes are reorganized, and only those same boxes may receive the result. Unselected boxes remain byte-for-byte outside the operation. Optional **Rename affected boxes** is unchecked by default. When enabled, localized PKHeX type names are used where available, names are safely fitted to the loaded save format, and every old-to-new name is part of the preview and organization plan.

The preview reports the mode, usable and used boxes, organized Pokémon, full and partial type boxes, mixed boxes, coherent and mixed Pokémon counts, unused slots, complete proposed layout, and box renames. Cancel changes nothing. Apply first verifies that every selected slot and box name still matches the preview, snapshots the save, writes the complete target layout, applies approved names, marks the in-memory save edited, and refreshes PKHeX. A failure restores the snapshot. Organizer Mod never saves the file to disk automatically.

## Living Dex Organizer

Choose **Living Dex Organizer** in the Organizer window to arrange Pokémon in National Pokédex order. The selected boxes are a single explicit safety boundary: their Pokémon are considered as sources and the same boxes may be rewritten as destinations. Unselected boxes are never read into the plan, renamed, cleared, or otherwise changed.

The first version provides:

- **Species Living Dex** — one representative per National Dex species. Any legitimate stored form can satisfy the species entry; extra copies go to overflow.
- **Form Living Dex** — one representative for each collectible stored form described by PKHeX. Base form comes first, followed by stable form index.
- **Shiny Living Dex** — one shiny representative per species or per collectible form. Non-shiny Pokémon remain present in overflow.

The expected scope is the full National Dex named by the checked-out PKHeX version. It is not filtered to species transferable into the currently loaded game because PKHeX does not expose that as one reliable cross-generation query. Form definitions come from PKHeX's current `FormConverter` metadata. Forms identified by PKHeX as battle-only or fused are excluded; Gigantamax state is not a form-index entry, and temporary transformations are not included. This policy is isolated in the PKHeX adapter so future compatibility corrections do not affect the domain planner.

When several Pokémon qualify for one entry, the representative preset is deterministic:

- **Default / Safest** prefers legal, current-trainer, non-egg, favorite/protected, higher-level, higher-IV, and more decorated candidates.
- **Oldest obtained** prefers legal candidates with the earliest valid met or egg date.
- **Strongest** prefers legal candidates by level, IV total, and completed EV investment.

Every preset ends with original box, slot, and stable identity tie-breakers. Legality and quality metadata are extracted once per Pokémon. Structurally corrupt or unrecognized Pokémon never fill entries; legality-invalid but recognized Pokémon are allowed only as lower-priority fallback candidates, matching the explicit legal-first representative policy.

Eggs never satisfy entries. Eggs and structurally invalid Pokémon can either remain included in overflow or be excluded and preserved in their original slots. Preserved slots reduce available capacity and are never cleared or selected as destinations. Other duplicates, excluded-form Pokémon, and non-shiny Pokémon in Shiny mode are packed into overflow in National Dex, original-position, or species-then-quality order.

Overflow can start immediately after the last filled entry or at the next selected box boundary. Boundary mode is the default and may require an additional selected box even when raw slot capacity is otherwise sufficient; an invalid plan is shown instead of silently changing this option.

The preview includes expected, filled, and missing entries, completion percentage, duplicates, overflow, preserved Pokémon, capacity, proposed boxes, warnings, and old-to-new box names. Missing entries include National Dex number, localized species name, form label, and shiny requirement, and the list can be copied. Missing entries do not reserve physical slots or create placeholder Pokémon.

Optional box renaming remains unchecked by default. Generated names are stable English (`Living Dex`, `Form Dex`, `Shiny Dex`, and `Overflow`), numbered when multiple boxes are used, and deterministically fitted to the save format's box-name limit. Renames are plan operations and are applied only after preview confirmation.

## Remove Duplicate Species

Choose **Remove Duplicate Species** from the Organizer window's **Function** selector, or use its direct Tools-menu shortcut. This is a standalone destructive function, not a sorting strategy: it scans only the selected boxes, keeps representatives in their existing slots, and clears only the duplicate slots approved in the preview. It never compacts or reorders the remaining Pokémon. Pokémon in unselected boxes do not participate and remain unchanged.

Duplicate identity is the stable National Dex species ID. In this initial version, alternate forms, genders, abilities, origins, moves, and other differences still count as the same species. The shiny selector changes grouping:

- **Consider shiny and non-shiny the same species** keeps one representative across both.
- **Treat shiny and non-shiny separately** may keep one shiny and one non-shiny representative per species.
- **Ignore shiny Pokémon** excludes every shiny Pokémon completely; its slot remains untouched.

Representative criteria are individually enabled and reordered with the arrow buttons. They are evaluated lexicographically in the displayed order, so a lower-priority criterion can never outweigh a higher-priority one:

- **Highest level** retains candidates with the highest current level.
- **Preferred origin game** prefers a match to one stable PKHeX game identifier. If no candidate matches, the tied set is unchanged.
- **Preferred gender** similarly prefers Male, Female, or Genderless only when a match exists.

If criteria remain tied—or all are disabled—the earliest original box and slot wins deterministically. Every other candidate remains visible as a removal candidate with the winning reasons. Eggs and structurally invalid or unrecognized entries are ignored and left in place.

The preview lists every KEEP and REMOVE decision, active criterion order, shiny behavior, and excluded counts. Continuing from the preview requires a second destructive confirmation whose default button is Cancel. Before clearing any slot, Organizer Mod verifies that the same save, selected slots, and box names still match the preview and creates an in-memory full-save snapshot. Failures restore that snapshot and refresh PKHeX. The save is not written to disk automatically.

Applying this function genuinely clears Pokémon slots. Always test with a copied save file and review the complete preview before confirming.

## Import from PKM Database

**Import from PKM Database** is a standalone function that reads the database path configured by the running PKHeX instance, scans it recursively, and compares supported Pokémon files with occupied slots in the selected boxes. Selected boxes are the complete comparison and mutation boundary: they provide conflict matches, explicit replacement targets, and empty import destinations. Party data and unselected boxes are not considered or changed.

Files are ordered by normalized relative path and parsed once using PKHeX's current entity-detection APIs. Unrelated file sizes are ignored; malformed supported-size files become preview warnings instead of aborting the scan. Each compatible entity is converted through PKHeX's supported `EntityConverter` route to the loaded save format. Missing or incompatible transfer routes are skipped and reported; speculative incompatible/reflection conversions are rejected.

Conflict resolution always applies in this order:

1. **Same PID** — import additionally, skip, or replace when the incoming Pokémon has a higher level (or equal level and higher experience). Replacement applies only to the same species. A matching PID on a different species imports additionally with a warning.
2. **Same species and shiny status** — import additionally, skip when an existing match exists, or choose the best database representative and replace a weaker save representative. Alternate forms share this species/shiny key in the current version.
3. Candidates without a conclusive conflict become new imports.

PID decisions are terminal and never pass through species conflict handling a second time. Multiple existing PID matches are reported; at most one deterministic target is replaced. The “best database representative” mode ranks by level, experience, then source path. It does not deduplicate other save Pokémon.

Database filters are combined with logical AND:

- unrestricted or PKHeX-verified legal only;
- optional stable origin-game ID;
- optional minimum level from 1 through 100;
- optional Male, Female, or Genderless value.

New imports use empty selected slots in box/slot order. Replacements do not consume empty capacity. If all planned imports do not fit, the plan is invalid and nothing can be applied. The preview separates new imports, prominent replacements, skipped entries, and scan warnings, and shows filter, compatibility, and capacity statistics.

After preview approval, a second confirmation defaults to Cancel. Applying revalidates the same save, every selected slot, box names, and the content hashes of source database files. Organizer Mod then creates a complete in-memory snapshot, applies replacements followed by imports, refreshes PKHeX, and restores the snapshot on any failure. It never saves the file to disk automatically.

PKHeX does not currently expose a single reliable shiny-lock catalog covering every species, form, encounter source, and generation. Shiny missing entries are therefore explicitly presented as collection coverage gaps, not assertions that each entry is obtainable. The preview repeats this limitation.

The **Tools > Organizer Mod > Remove dupplicates** command removes repeated party or box Pokémon that share both a PID and species. Pension Pokémon are included in the search but are read-only and always kept ahead of party or box candidates. Otherwise, the command keeps the highest-level Pokémon, then the one with the most EXP, then a party member; a true final tie is resolved randomly.

Before mutation, a resizable, scrollable review table shows every Pokémon to be deleted, its exact team/box location, the Pokémon being kept, and the relevant level, EXP, and priority differences. Explicit confirmation is required. Generation 1 and 2 saves are not supported because those formats do not have meaningful PIDs.

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

Organizer Mod is experimental. Test only with copied save files and keep known-good backups. Duplicate removal and type allocation mutate the in-memory save only after confirmation; use PKHeX's normal save/export workflow to persist changes. Organization behavior calculates and previews a complete target state before mutation and never overwrites an occupied slot without first preserving its planned data in the immutable plan snapshot.

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
