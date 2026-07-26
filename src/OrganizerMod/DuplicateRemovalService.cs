using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed record DuplicateRemovalSession(
    SaveFile Save,
    DuplicateRemovalPlan Plan);

internal sealed class DuplicateRemovalService(ISaveFileProvider saveFileProvider)
{
    public DuplicateRemovalSession CreateSession()
    {
        var save = saveFileProvider.SAV;
        if (save.Generation < 3)
        {
            throw new NotSupportedException(
                "Generation 1 and 2 Pokémon do not have meaningful personality IDs, so duplicate removal is unavailable for this save.");
        }

        var plan = DuplicateRemovalPlanner.CreatePlan(ReadCandidates(save), Random.Shared);
        ValidatePlanStillMatches(save, plan);
        return new DuplicateRemovalSession(save, plan);
    }

    public DuplicateRemovalPlan CreatePlan() => CreateSession().Plan;

    public void Apply(DuplicateRemovalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var save = saveFileProvider.SAV;
        if (!ReferenceEquals(save, session.Save))
            throw new InvalidOperationException(
                "A different save was loaded after the preview. No duplicates were removed.");

        ValidatePlanStillMatches(save, session.Plan);
        var backup = save.Clone();
        var wasEdited = save.State.Edited;
        try
        {
            ApplyRemovals(save, session.Plan);
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

    public void Apply(DuplicateRemovalPlan plan) =>
        Apply(new DuplicateRemovalSession(saveFileProvider.SAV, plan));

    private static void ApplyRemovals(SaveFile save, DuplicateRemovalPlan plan)
    {
        foreach (var removal in plan.Removals
                     .Where(item => !item.Removed.Location.IsParty))
        {
            var location = removal.Removed.Location;
            save.SetBoxSlotAtIndex(
                save.BlankPKM,
                location.Box,
                location.Slot,
                EntityImportSettings.None);
        }

        foreach (var partySlot in plan.Removals
                     .Where(item => item.Removed.Location.IsParty)
                     .Select(item => item.Removed.Location.Slot)
                     .OrderDescending())
        {
            save.DeletePartySlot(partySlot);
        }
    }

    private static IReadOnlyList<DuplicatePokemon> ReadCandidates(SaveFile save)
    {
        var candidates = new List<DuplicatePokemon>(save.PartyCount + save.SlotCount);

        if (save.HasParty)
        {
            for (var slot = 0; slot < save.PartyCount; slot++)
            {
                AddCandidate(
                    candidates,
                    save.GetPartySlotAtIndex(slot),
                    PokemonStorageLocation.Party(slot));
            }
        }

        if (save.HasBox)
        {
            for (var box = 0; box < save.BoxCount; box++)
            {
                for (var slot = 0; slot < save.BoxSlotCount; slot++)
                {
                    // Some saves store party members in ordinary box storage and
                    // expose them through both APIs. Count the party view only.
                    if (save.GetBoxSlotFlags(box, slot).IsParty() >= 0)
                        continue;

                    AddCandidate(
                        candidates,
                        save.GetBoxSlotAtIndex(box, slot),
                        PokemonStorageLocation.BoxSlot(box, slot));
                }
            }
        }

        ReadPensionCandidates(save, candidates);
        return candidates;
    }

    private static void ReadPensionCandidates(
        SaveFile save,
        ICollection<DuplicatePokemon> candidates)
    {
        var facility = 0;
        if (save is IDaycareMulti multiplePensions)
        {
            for (var index = 0; index < multiplePensions.DaycareCount; index++)
            {
                ReadPension(
                    save,
                    multiplePensions[index],
                    facility++,
                    candidates);
            }
        }
        else if (save is IDaycareStorage pension)
        {
            ReadPension(save, pension, facility++, candidates);
        }

        var extraPensionSlots = save
            .GetExtraSlots()
            .Where(slot => slot.Type == StorageSlotType.Daycare)
            .ToArray();
        for (var slot = 0; slot < extraPensionSlots.Length; slot++)
        {
            AddCandidate(
                candidates,
                extraPensionSlots[slot].Read(save),
                PokemonStorageLocation.Pension(facility, slot));
        }
    }

    private static void ReadPension(
        SaveFile save,
        IDaycareStorage pension,
        int facility,
        ICollection<DuplicatePokemon> candidates)
    {
        for (var slot = 0; slot < pension.DaycareSlotCount; slot++)
        {
            if (!pension.IsDaycareOccupied(slot))
                continue;

            AddCandidate(
                candidates,
                save.GetStoredSlot(pension.GetDaycareSlot(slot).Span),
                PokemonStorageLocation.Pension(facility, slot));
        }
    }

    private static void AddCandidate(
        ICollection<DuplicatePokemon> candidates,
        PKM pokemon,
        PokemonStorageLocation location)
    {
        if (pokemon.Species == 0)
            return;

        candidates.Add(
            new DuplicatePokemon(
                pokemon.PID,
                pokemon.Species,
                pokemon.CurrentLevel,
                pokemon.EXP,
                location));
    }

    private static void ValidatePlanStillMatches(SaveFile save, DuplicateRemovalPlan plan)
    {
        var currentCandidates = ReadCandidates(save)
            .ToDictionary(candidate => candidate.Location);
        var plannedCandidates = plan.Removals
            .SelectMany(removal => new[] { removal.Kept, removal.Removed })
            .DistinctBy(candidate => candidate.Location);

        foreach (var candidate in plannedCandidates)
        {
            var location = candidate.Location;
            if (!currentCandidates.TryGetValue(location, out var current) ||
                current.Species != candidate.Species ||
                current.PersonalityId != candidate.PersonalityId ||
                current.Level != candidate.Level ||
                current.Experience != candidate.Experience)
            {
                throw new InvalidOperationException(
                    $"The Pokémon at {DescribeLocation(location)} changed after the preview. No duplicates were removed.");
            }
        }

        foreach (var removal in plan.Removals)
        {
            var location = removal.Removed.Location;
            if (!location.IsParty && save.IsBoxSlotOverwriteProtected(location.Box, location.Slot))
            {
                throw new InvalidOperationException(
                    $"The duplicate at {DescribeLocation(location)} is locked or belongs to a battle team. No duplicates were removed.");
            }
        }
    }

    private static string DescribeLocation(PokemonStorageLocation location) =>
        location.Area switch
        {
            PokemonStorageArea.Party => $"party slot {location.Slot + 1}",
            PokemonStorageArea.Box => $"box {location.Box + 1}, slot {location.Slot + 1}",
            PokemonStorageArea.Pension => $"pension {location.Facility + 1}, slot {location.Slot + 1}",
            _ => throw new ArgumentOutOfRangeException(nameof(location)),
        };
}
