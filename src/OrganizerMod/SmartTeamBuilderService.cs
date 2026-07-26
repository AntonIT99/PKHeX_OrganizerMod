using System.Collections.ObjectModel;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class SmartTeamBuilderSession(
    SaveFile save,
    TeamExchangePlan plan,
    IReadOnlyDictionary<string, PKM> pokemonSnapshots,
    IReadOnlyDictionary<(int Box, int Slot), string> boxFingerprints,
    IReadOnlyDictionary<int, string> teamFingerprints,
    int teamCount,
    IReadOnlyDictionary<int, string> boxNames,
    IReadOnlyDictionary<string, TeamBuilderCandidate> candidates)
{
    public SaveFile Save { get; } = save;
    public TeamExchangePlan Plan { get; } = plan;
    public IReadOnlyDictionary<string, PKM> PokemonSnapshots { get; } = pokemonSnapshots;
    public IReadOnlyDictionary<(int Box, int Slot), string> BoxFingerprints { get; } = boxFingerprints;
    public IReadOnlyDictionary<int, string> TeamFingerprints { get; } = teamFingerprints;
    public int TeamCount { get; } = teamCount;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
    public IReadOnlyDictionary<string, TeamBuilderCandidate> Candidates { get; } = candidates;
}

internal static class SpeciesIntroductionGeneration
{
    public static int Get(ushort species) => species switch
    {
        <= 151 => 1,
        <= 251 => 2,
        <= 386 => 3,
        <= 493 => 4,
        <= 649 => 5,
        <= 721 => 6,
        <= 809 => 7,
        <= 905 => 8,
        _ => 9,
    };
}

internal sealed class SmartTeamBuilderService(ISaveFileProvider saveFileProvider)
{
    private readonly SmartTeamBuilderPlanner planner = new();

    public IReadOnlyList<BoxSelectionItem> GetBoxSelection() =>
        new LivingDexOrganizationService(saveFileProvider).GetBoxSelection();

    public SmartTeamBuilderSession CreatePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        Func<IReadOnlySet<int>, int, TeamBuilderOptions> createOptions)
    {
        var save = saveFileProvider.SAV;
        if (!save.HasParty)
            throw new NotSupportedException("The loaded save does not provide a writable Team.");
        if (!save.HasBox)
            throw new NotSupportedException("The loaded save does not provide storage boxes.");
        var available = GetBoxSelection().ToDictionary(x => x.BoxIndex);
        var selected = selectedBoxIndices.Distinct().Order().ToArray();
        if (selected.Length == 0)
            throw new InvalidOperationException("Select at least one storage box.");
        foreach (var box in selected)
            if (!available.TryGetValue(box, out var item) || !item.IsAvailable)
                throw new InvalidOperationException($"Box {box + 1} is unavailable.");

        var candidates = new List<TeamBuilderCandidate>();
        var snapshots = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var teamFingerprints = new Dictionary<int, string>();
        var boxFingerprints = new Dictionary<(int, int), string>();
        var empty = new List<SlotPosition>();
        for (var slot = 0; slot < save.PartyCount; slot++)
        {
            var entity = save.GetPartySlotAtIndex(slot);
            var fingerprint = OrganizationStorageUtilities.Fingerprint(entity);
            teamFingerprints[slot] = fingerprint;
            if (entity.Species == 0) continue;
            Add(entity, PokemonStorageLocation.Party(slot), $"team:{slot:D2}:{fingerprint}", candidates, snapshots);
        }
        foreach (var box in selected)
        for (var slot = 0; slot < save.BoxSlotCount; slot++)
        {
            var entity = save.GetBoxSlotAtIndex(box, slot);
            var fingerprint = OrganizationStorageUtilities.Fingerprint(entity);
            boxFingerprints[(box, slot)] = fingerprint;
            if (entity.Species == 0) { empty.Add(new SlotPosition(box, slot)); continue; }
            Add(entity, PokemonStorageLocation.BoxSlot(box, slot), $"{box:D3}:{slot:D2}:{fingerprint}", candidates, snapshots);
        }
        var selectedSet = new HashSet<int>(selected);
        var options = createOptions(selectedSet, 6);
        var plan = planner.CreatePlan(candidates, empty, options);
        var names = selected.ToDictionary(box => box, box => OrganizationStorageUtilities.GetBoxName(save, box));
        return new SmartTeamBuilderSession(save, plan,
            new ReadOnlyDictionary<string, PKM>(snapshots),
            new ReadOnlyDictionary<(int, int), string>(boxFingerprints),
            new ReadOnlyDictionary<int, string>(teamFingerprints),
            save.PartyCount,
            new ReadOnlyDictionary<int, string>(names),
            new ReadOnlyDictionary<string, TeamBuilderCandidate>(
                candidates.ToDictionary(x => x.StableId, StringComparer.Ordinal)));
    }

    public void Apply(SmartTeamBuilderSession session)
    {
        if (!session.Plan.IsValid) throw new InvalidOperationException("An invalid Team plan cannot be applied.");
        var save = saveFileProvider.SAV;
        if (!ReferenceEquals(save, session.Save))
            throw new InvalidOperationException("A different save was loaded after the preview. Generate a new preview.");
        SafeOrganizationApplier.ValidateStillMatches(save, session.BoxFingerprints, session.BoxNames);
        if (save.PartyCount != session.TeamCount)
            throw new InvalidOperationException("The Team size changed after the preview. Generate a new preview.");
        foreach (var pair in session.TeamFingerprints)
            if (OrganizationStorageUtilities.Fingerprint(save.GetPartySlotAtIndex(pair.Key)) != pair.Value)
                throw new InvalidOperationException($"Team slot {pair.Key + 1} changed after the preview. Generate a new preview.");

        var backup = save.Clone();
        var wasEdited = save.State.Edited;
        try
        {
            foreach (var box in session.Plan.Options.SelectedBoxIndices)
                for (var slot = 0; slot < save.BoxSlotCount; slot++)
                    save.SetBoxSlotAtIndex(save.BlankPKM, box, slot, EntityImportSettings.None);
            foreach (var assignment in session.Plan.FinalBoxAssignments)
                save.SetBoxSlotAtIndex(GetSnapshot(session, assignment.StableId), assignment.BoxIndex, assignment.SlotIndex, EntityImportSettings.None);

            for (var slot = 0; slot < 6; slot++)
                save.SetPartySlotAtIndex(save.BlankPKM, slot, EntityImportSettings.None);
            foreach (var decision in session.Plan.SelectedTeam.OrderBy(x => x.FinalTeamSlot))
                save.SetPartySlotAtIndex(GetSnapshot(session, decision.StableId), decision.FinalTeamSlot, EntityImportSettings.None);

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

    private static PKM GetSnapshot(SmartTeamBuilderSession session, string stableId) =>
        session.PokemonSnapshots.TryGetValue(stableId, out var entity)
            ? entity.Clone()
            : throw new InvalidOperationException($"The snapshot for {stableId} is missing.");

    private static void Add(PKM entity, PokemonStorageLocation location, string stableId,
        ICollection<TeamBuilderCandidate> candidates, IDictionary<string, PKM> snapshots)
    {
        var type1 = ToType(entity.PersonalInfo.Type1);
        var type2 = ToType(entity.PersonalInfo.Type2);
        var speciesNames = GameInfo.Strings.Species;
        var gameNames = GameInfo.Strings.gamelist;
        var origin = (int)entity.Version;
        var valid = entity.Valid && entity.Species < speciesNames.Count && entity.CurrentLevel is >= 1 and <= 100 &&
                    type1 is not null && type2 is not null;
        candidates.Add(new TeamBuilderCandidate(stableId, location, entity.Species, entity.Form,
            entity.Species < speciesNames.Count ? speciesNames[entity.Species] : $"Species {entity.Species}",
            entity.CurrentLevel, entity.EXP, type1 ?? PokemonElementType.Normal,
            type2 == type1 ? null : type2, origin,
            origin >= 0 && origin < gameNames.Length ? gameNames[origin] : "Unknown",
            SpeciesIntroductionGeneration.Get(entity.Species),
            SpeciesCategory.IsLegendary(entity.Species) || SpeciesCategory.IsSubLegendary(entity.Species) ||
            SpeciesCategory.IsMythical(entity.Species),
            entity.IsShiny, entity.IsEgg, valid));
        snapshots.Add(stableId, entity.Clone());
    }

    private static PokemonElementType? ToType(byte value) =>
        value <= (byte)PokemonElementType.Fairy ? (PokemonElementType)value : null;
}
