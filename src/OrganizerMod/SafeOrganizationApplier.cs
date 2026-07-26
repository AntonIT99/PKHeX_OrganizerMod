using System.Security.Cryptography;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal static class OrganizationStorageUtilities
{
    public static string Fingerprint(PKM entity) =>
        Convert.ToHexString(SHA256.HashData(entity.Data[..entity.SIZE_STORED]));

    public static string GetBoxName(SaveFile save, int box) =>
        save is IBoxDetailNameRead names
            ? names.GetBoxName(box)
            : BoxDetailNameExtensions.GetDefaultBoxName(box);

    public static int GetMaximumBoxNameLength(SaveFile save) =>
        save.Generation switch
        {
            2 when save is SAV2 { Japanese: false, Korean: false } => 16,
            3 when save is SAV3RSBox => 8 + SAV3RSBox.BoxNamePrefix,
            6 or 7 => 14,
            >= 8 => 16,
            _ => 8,
        };
}

internal static class SafeOrganizationApplier
{
    public static void Apply(
        ISaveFileProvider saveFileProvider,
        SaveFile expectedSave,
        IReadOnlyList<int> selectedBoxes,
        IReadOnlyDictionary<string, PKM> pokemonSnapshots,
        IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
        IReadOnlyDictionary<int, string> originalBoxNames,
        IEnumerable<(PokemonReference Pokemon, int TargetBox, int TargetSlot)> assignments,
        IReadOnlyList<BoxRenameOperation> renames,
        IReadOnlySet<(int Box, int Slot)> preservedSlots,
        IReadOnlyDictionary<int, int>? originalBackgrounds = null,
        IReadOnlyList<BoxBackgroundChangeOperation>? backgroundChanges = null)
    {
        var save = saveFileProvider.SAV;
        if (!ReferenceEquals(save, expectedSave))
            throw new InvalidOperationException("A different save was loaded after the preview. Nothing was changed.");
        ValidateStillMatches(save, slotFingerprints, originalBoxNames);
        ValidateBackgroundsStillMatch(save, originalBackgrounds);

        var materializedAssignments = assignments.ToArray();
        var backup = save.Clone();
        var wasEdited = save.State.Edited;
        try
        {
            foreach (var box in selectedBoxes)
            {
                for (var slot = 0; slot < save.BoxSlotCount; slot++)
                {
                    if (preservedSlots.Contains((box, slot)))
                        continue;
                    save.SetBoxSlotAtIndex(
                        save.BlankPKM,
                        box,
                        slot,
                        EntityImportSettings.None);
                }
            }

            foreach (var assignment in materializedAssignments)
            {
                if (preservedSlots.Contains((assignment.TargetBox, assignment.TargetSlot)))
                    throw new InvalidOperationException("The organization plan attempted to overwrite a preserved slot.");
                if (!pokemonSnapshots.TryGetValue(assignment.Pokemon.StableId, out var snapshot))
                    throw new InvalidOperationException("A Pokémon snapshot required by the organization plan is missing.");
                save.SetBoxSlotAtIndex(
                    snapshot.Clone(),
                    assignment.TargetBox,
                    assignment.TargetSlot,
                    EntityImportSettings.None);
            }

            if (renames.Count != 0)
            {
                if (save is not IBoxDetailName boxNames)
                    throw new InvalidOperationException("The save no longer supports box renaming.");
                foreach (var rename in renames)
                    boxNames.SetBoxName(rename.BoxIndex, rename.NewName);
            }

            if (backgroundChanges is { Count: not 0 })
            {
                var catalog = new BoxBackgroundCatalog(save);
                if (!catalog.CanAssign)
                    throw new InvalidOperationException("The save no longer supports writable mapped box backgrounds.");
                foreach (var change in backgroundChanges)
                {
                    try
                    {
                        catalog.SetWallpaper(change.BoxIndex, change.NewWallpaperId);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Box {change.BoxIndex + 1}: background \"{change.NewDisplayName}\" could not be applied.", ex);
                    }
                }
            }

            save.State.Edited = true;
            saveFileProvider.ReloadSlots();
        }
        catch
        {
            save.CopyChangesFrom(backup);
            save.State.Edited = wasEdited;
            saveFileProvider.ReloadSlots();
            throw;
        }
    }

    private static void ValidateBackgroundsStillMatch(
        SaveFile save,
        IReadOnlyDictionary<int, int>? originalBackgrounds)
    {
        if (originalBackgrounds is not { Count: not 0 })
            return;
        var catalog = new BoxBackgroundCatalog(save);
        if (!catalog.CanAssign)
            throw new InvalidOperationException("The save no longer supports writable mapped box backgrounds.");
        foreach (var pair in originalBackgrounds)
        {
            if (catalog.GetCurrentWallpaper(pair.Key) != pair.Value)
            {
                throw new InvalidOperationException(
                    $"The background of box {pair.Key + 1} changed after the preview. Nothing was changed.");
            }
        }
    }

    internal static void ValidateStillMatches(
        SaveFile save,
        IReadOnlyDictionary<(int Box, int Slot), string> fingerprints,
        IReadOnlyDictionary<int, string> boxNames)
    {
        foreach (var pair in fingerprints)
        {
            var current = save.GetBoxSlotAtIndex(pair.Key.Box, pair.Key.Slot);
            if (!string.Equals(
                    OrganizationStorageUtilities.Fingerprint(current),
                    pair.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Box {pair.Key.Box + 1}, slot {pair.Key.Slot + 1} changed after the preview. Nothing was changed.");
            }
        }

        foreach (var pair in boxNames)
        {
            if (!string.Equals(
                    OrganizationStorageUtilities.GetBoxName(save, pair.Key),
                    pair.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The name of box {pair.Key + 1} changed after the preview. Nothing was changed.");
            }
        }
    }
}

internal static class SafeRemovalApplier
{
    public static void Apply(
        ISaveFileProvider saveFileProvider,
        SaveFile expectedSave,
        IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
        IReadOnlyDictionary<int, string> originalBoxNames,
        IReadOnlyCollection<DuplicateCandidate> removals)
    {
        var save = saveFileProvider.SAV;
        if (!ReferenceEquals(save, expectedSave))
            throw new InvalidOperationException("A different save was loaded after the preview. Nothing was changed.");
        SafeOrganizationApplier.ValidateStillMatches(save, slotFingerprints, originalBoxNames);

        var targets = removals
            .Select(item => (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex))
            .ToArray();
        if (targets.Distinct().Count() != targets.Length)
            throw new InvalidOperationException("The removal plan contains a duplicate storage slot.");

        var backup = save.Clone();
        var wasEdited = save.State.Edited;
        try
        {
            foreach (var target in targets)
            {
                save.SetBoxSlotAtIndex(
                    save.BlankPKM,
                    target.SourceBoxIndex,
                    target.SourceSlotIndex,
                    EntityImportSettings.None);
            }
            save.State.Edited = true;
            saveFileProvider.ReloadSlots();
        }
        catch
        {
            save.CopyChangesFrom(backup);
            save.State.Edited = wasEdited;
            saveFileProvider.ReloadSlots();
            throw;
        }
    }
}
