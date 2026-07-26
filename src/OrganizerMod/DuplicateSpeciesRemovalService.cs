using System.Collections.ObjectModel;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed record OriginGameChoice(int Id, string Name)
{
    public override string ToString() => Name;
}

internal sealed class DuplicateSpeciesRemovalSession(
    SaveFile save,
    SpeciesDuplicateRemovalPlan plan,
    IReadOnlyList<int> selectedBoxes,
    IReadOnlyDictionary<string, PKM> pokemonSnapshots,
    IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
    IReadOnlyDictionary<int, string> boxNames,
    IReadOnlyDictionary<int, string> originGameNames)
{
    public SaveFile Save { get; } = save;
    public SpeciesDuplicateRemovalPlan Plan { get; } = plan;
    public IReadOnlyList<int> SelectedBoxes { get; } = selectedBoxes;
    public IReadOnlyDictionary<string, PKM> PokemonSnapshots { get; } = pokemonSnapshots;
    public IReadOnlyDictionary<(int Box, int Slot), string> SlotFingerprints { get; } = slotFingerprints;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
    public IReadOnlyDictionary<int, string> OriginGameNames { get; } = originGameNames;
}

internal sealed class DuplicateSpeciesRemovalService(ISaveFileProvider saveFileProvider)
{
    private readonly DuplicateSpeciesPlanner planner = new();

    public IReadOnlyList<BoxSelectionItem> GetBoxSelection() =>
        new LivingDexOrganizationService(saveFileProvider).GetBoxSelection();

    public static IReadOnlyList<OriginGameChoice> GetOriginGames()
    {
        var names = GameInfo.Strings.gamelist;
        return GameUtil.GameVersions
            .Select(version => new OriginGameChoice(
                (int)version,
                (int)version < names.Length && !string.IsNullOrWhiteSpace(names[(int)version])
                    ? names[(int)version]
                    : version.ToString()))
            .OrderBy(item => item.Id)
            .ToArray();
    }

    public DuplicateSpeciesRemovalSession CreatePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        ShinyDuplicateMode shinyMode,
        IReadOnlyList<DuplicateSelectionCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(selectedBoxIndices);
        ArgumentNullException.ThrowIfNull(criteria);
        var save = GetSupportedSave();
        if (selectedBoxIndices.Count == 0)
            throw new InvalidOperationException("Select at least one available box to scan.");

        var choices = GetBoxSelection().ToDictionary(item => item.BoxIndex);
        var selected = selectedBoxIndices.Distinct().Order().ToArray();
        foreach (var box in selected)
        {
            if (!choices.TryGetValue(box, out var choice))
                throw new ArgumentOutOfRangeException(nameof(selectedBoxIndices), $"Box {box + 1} does not exist.");
            if (!choice.IsAvailable)
                throw new InvalidOperationException($"Box {box + 1} is unavailable because it {choice.UnavailableReason}.");
        }

        var games = GetOriginGames();
        var supported = games.Select(item => item.Id).ToHashSet();
        var candidates = new List<DuplicateCandidate>();
        var snapshots = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var fingerprints = new Dictionary<(int Box, int Slot), string>();
        var boxNames = selected.ToDictionary(
            box => box,
            box => OrganizationStorageUtilities.GetBoxName(save, box));
        var maximumSpecies = GameInfo.Strings.Species.Count - 1;
        foreach (var box in selected)
        {
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                var entity = save.GetBoxSlotAtIndex(box, slot);
                var fingerprint = OrganizationStorageUtilities.Fingerprint(entity);
                fingerprints[(box, slot)] = fingerprint;
                if (entity.Species == 0)
                    continue;
                var stableId = $"{box:D3}:{slot:D2}:{fingerprint}";
                var candidate = new DuplicateCandidate(
                    new PokemonReference(stableId, box, slot),
                    entity.Species,
                    entity.Form,
                    entity.IsShiny,
                    entity.CurrentLevel,
                    (int)entity.Version,
                    entity.Gender switch
                    {
                        0 => PokemonGenderPreference.Male,
                        1 => PokemonGenderPreference.Female,
                        _ => PokemonGenderPreference.Genderless,
                    },
                    entity.IsEgg,
                    entity.Valid && entity.Species <= maximumSpecies);
                candidates.Add(candidate);
                snapshots.Add(stableId, entity.Clone());
            }
        }

        var options = new DuplicateSpeciesOptions(
            shinyMode,
            Array.AsReadOnly(criteria.ToArray()),
            new HashSet<int>(selected),
            supported);
        var plan = planner.CreatePlan(candidates, options);
        return new DuplicateSpeciesRemovalSession(
            save,
            plan,
            selected,
            new ReadOnlyDictionary<string, PKM>(snapshots),
            new ReadOnlyDictionary<(int Box, int Slot), string>(fingerprints),
            new ReadOnlyDictionary<int, string>(boxNames),
            new ReadOnlyDictionary<int, string>(games.ToDictionary(item => item.Id, item => item.Name)));
    }

    public void Apply(DuplicateSpeciesRemovalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Plan.IsValid)
            throw new InvalidOperationException("An invalid duplicate-removal plan cannot be applied.");
        SafeRemovalApplier.Apply(
            saveFileProvider,
            session.Save,
            session.SlotFingerprints,
            session.BoxNames,
            session.Plan.RemovalCandidates);
    }

    private SaveFile GetSupportedSave()
    {
        var save = saveFileProvider.SAV;
        if (!save.HasBox)
            throw new NotSupportedException("The loaded save does not provide storage boxes.");
        return save;
    }
}
