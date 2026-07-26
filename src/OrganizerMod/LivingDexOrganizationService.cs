using System.Collections.ObjectModel;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class LivingDexOrganizationSession(
    SaveFile save,
    LivingDexOrganizationPlan plan,
    IReadOnlyList<int> selectedBoxes,
    IReadOnlyDictionary<string, PKM> pokemonSnapshots,
    IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
    IReadOnlyDictionary<int, string> boxNames,
    string definitionScope)
{
    public SaveFile Save { get; } = save;
    public LivingDexOrganizationPlan Plan { get; } = plan;
    public IReadOnlyList<int> SelectedBoxes { get; } = selectedBoxes;
    public IReadOnlyDictionary<string, PKM> PokemonSnapshots { get; } = pokemonSnapshots;
    public IReadOnlyDictionary<(int Box, int Slot), string> SlotFingerprints { get; } = slotFingerprints;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
    public string DefinitionScope { get; } = definitionScope;
}

internal sealed class LivingDexOrganizationService(ISaveFileProvider saveFileProvider)
{
    private readonly LivingDexOrganizationPlanner planner = new();

    public bool CanRenameBoxes => saveFileProvider.SAV is IBoxDetailName;

    public IReadOnlyList<BoxSelectionItem> GetBoxSelection()
    {
        var save = GetSupportedSave();
        var result = new List<BoxSelectionItem>(save.BoxCount);
        for (var box = 0; box < save.BoxCount; box++)
        {
            var count = 0;
            string? reason = null;
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                var flags = save.GetBoxSlotFlags(box, slot);
                if (flags.IsOverwriteProtected())
                    reason ??= "contains a locked, reserved, or battle-team slot";
                if (flags.IsParty() >= 0)
                    reason ??= "shares storage with the party";
                if (save.GetBoxSlotAtIndex(box, slot).Species != 0)
                    count++;
            }

            result.Add(new BoxSelectionItem(
                box,
                OrganizationStorageUtilities.GetBoxName(save, box),
                count,
                reason is null,
                reason));
        }
        return result;
    }

    public LivingDexOrganizationSession CreatePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        LivingDexOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(selectedBoxIndices);
        ArgumentNullException.ThrowIfNull(options);
        var save = GetSupportedSave();
        if (selectedBoxIndices.Count == 0)
            throw new InvalidOperationException("Select at least one available box to organize.");
        if (options.RenameBoxes && save is not IBoxDetailName)
            throw new NotSupportedException("The loaded save format does not support writable box names.");

        var choices = GetBoxSelection().ToDictionary(item => item.BoxIndex);
        var selected = selectedBoxIndices.Distinct().Order().ToArray();
        foreach (var box in selected)
        {
            if (!choices.TryGetValue(box, out var choice))
                throw new ArgumentOutOfRangeException(nameof(selectedBoxIndices), $"Box {box + 1} does not exist.");
            if (!choice.IsAvailable)
                throw new InvalidOperationException($"Box {box + 1} is unavailable because it {choice.UnavailableReason}.");
        }

        var definitions = LivingDexDefinitionProvider.CreateDefinitions(
            save,
            options.Mode,
            options.ShinyScope);
        var maximumSpecies = GameInfo.Strings.Species.Count - 1;
        var pokemon = new List<LivingDexCandidate>();
        var snapshots = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var fingerprints = new Dictionary<(int Box, int Slot), string>();
        var boxNames = selected.ToDictionary(
            box => box,
            box => OrganizationStorageUtilities.GetBoxName(save, box));

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
                var reference = new PokemonReference(stableId, box, slot);
                var formNames = entity.Species <= maximumSpecies
                    ? LivingDexDefinitionProvider.GetFormNames(entity.Species, save.Context)
                    : [];
                var recognizedForm = entity.Form < formNames.Length ||
                                     FormInfo.IsValidOutOfBoundsForm(
                                         entity.Species,
                                         entity.Form,
                                         entity.Format);
                var hasValidData = entity.Valid &&
                                   entity.Species <= maximumSpecies &&
                                   recognizedForm;
                var isLegal = false;
                try
                {
                    isLegal = new LegalityAnalysis(entity, save.Personal).Valid;
                }
                catch
                {
                    // A failed analysis is neutral metadata for the domain and is
                    // computed only once for this Pokémon.
                }

                var ribbonOrMarkCount = 0;
                try
                {
                    ribbonOrMarkCount = RibbonInfo.GetRibbonInfo(entity)
                        .Sum(item => item.RibbonCount);
                }
                catch
                {
                    // Older formats do not expose every ribbon interface.
                }

                pokemon.Add(new LivingDexCandidate(
                    reference,
                    entity.Species,
                    entity.Form,
                    entity.IsShiny,
                    entity.IsEgg,
                    hasValidData,
                    isLegal,
                    entity.ID32 == save.ID32 &&
                    string.Equals(
                        entity.OriginalTrainerName,
                        save.OT,
                        StringComparison.Ordinal),
                    entity is IFavorite { IsFavorite: true },
                    entity.CurrentLevel,
                    entity.IVTotal,
                    entity.EVTotal,
                    ribbonOrMarkCount,
                    entity.MetDate ?? entity.EggMetDate));
                snapshots.Add(stableId, entity.Clone());
            }
        }

        var boxes = selected
            .Select(box => new BoxState(box, boxNames[box], save.BoxSlotCount))
            .ToArray();
        var plan = planner.CreatePlan(pokemon, definitions, boxes, options);
        return new LivingDexOrganizationSession(
            save,
            plan,
            selected,
            new ReadOnlyDictionary<string, PKM>(snapshots),
            new ReadOnlyDictionary<(int Box, int Slot), string>(fingerprints),
            new ReadOnlyDictionary<int, string>(boxNames),
            $"Full National Dex known to PKHeX ({definitions.Select(item => item.Key.Species).Distinct().Count()} species); transfer compatibility with the loaded game is not filtered.");
    }

    public void Apply(LivingDexOrganizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Plan.IsValid)
            throw new InvalidOperationException("An invalid Living Dex plan cannot be applied.");

        var preserved = session.Plan.PreservedPokemon
            .Select(item => (item.SourceBoxIndex, item.SourceSlotIndex))
            .ToHashSet();
        SafeOrganizationApplier.Apply(
            saveFileProvider,
            session.Save,
            session.SelectedBoxes,
            session.PokemonSnapshots,
            session.SlotFingerprints,
            session.BoxNames,
            session.Plan.Assignments.Select(item =>
                (item.Pokemon, item.TargetBoxIndex, item.TargetSlotIndex)),
            session.Plan.RenameOperations,
            preserved);
    }

    private SaveFile GetSupportedSave()
    {
        var save = saveFileProvider.SAV;
        if (!save.HasBox)
            throw new NotSupportedException("The loaded save does not provide storage boxes.");
        if (save.BoxSlotCount != LivingDexOrganizationPlanner.BoxCapacity)
        {
            throw new NotSupportedException(
                $"Living Dex Organizer requires 30-slot boxes; this save uses {save.BoxSlotCount} slots per box.");
        }
        return save;
    }
}
