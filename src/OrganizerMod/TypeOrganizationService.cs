using System.Collections.ObjectModel;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed record BoxSelectionItem(
    int BoxIndex,
    string Name,
    int PokemonCount,
    bool IsAvailable,
    string? UnavailableReason)
{
    public override string ToString()
    {
        var count = $"{PokemonCount}/30";
        var occupancy = PokemonCount == 0 ? " (empty)" : string.Empty;
        return IsAvailable
            ? $"Box {BoxIndex + 1}: {Name} — {count}{occupancy}"
            : $"Box {BoxIndex + 1}: {Name} — {count}{occupancy} — unavailable: {UnavailableReason}";
    }
}

internal sealed class TypeOrganizationSession(
    SaveFile save,
    TypeOrganizationPlan plan,
    IReadOnlyList<int> selectedBoxes,
    IReadOnlyDictionary<string, PKM> pokemonSnapshots,
    IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
    IReadOnlyDictionary<int, string> boxNames)
{
    public SaveFile Save { get; } = save;
    public TypeOrganizationPlan Plan { get; } = plan;
    public IReadOnlyList<int> SelectedBoxes { get; } = selectedBoxes;
    public IReadOnlyDictionary<string, PKM> PokemonSnapshots { get; } = pokemonSnapshots;
    public IReadOnlyDictionary<(int Box, int Slot), string> SlotFingerprints { get; } = slotFingerprints;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
}

internal sealed class TypeOrganizationService(ISaveFileProvider saveFileProvider)
{
    private readonly TypeBoxOrganizationPlanner planner = new();

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

                var entity = save.GetBoxSlotAtIndex(box, slot);
                if (entity.Species == 0)
                    continue;
                count++;
                if (!TryGetType(entity.PersonalInfo.Type1, out _) ||
                    !TryGetType(entity.PersonalInfo.Type2, out _))
                {
                    reason ??= "contains Pokémon with unsupported type data";
                }
            }

            result.Add(new BoxSelectionItem(
                box,
                GetBoxName(save, box),
                count,
                reason is null,
                reason));
        }

        return result;
    }

    public TypeOrganizationSession CreatePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        TypeBoxLayoutMode mode,
        bool renameBoxes)
    {
        ArgumentNullException.ThrowIfNull(selectedBoxIndices);
        var save = GetSupportedSave();
        if (selectedBoxIndices.Count == 0)
            throw new InvalidOperationException("Select at least one available box to organize.");
        if (renameBoxes && save is not IBoxDetailName)
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

        var pokemon = new List<OrganizablePokemon>();
        var snapshots = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var fingerprints = new Dictionary<(int Box, int Slot), string>();
        var boxNames = selected.ToDictionary(box => box, box => GetBoxName(save, box));

        foreach (var box in selected)
        {
            var boxEntities = Enumerable.Range(0, save.BoxSlotCount)
                .Select(slot => save.GetBoxSlotAtIndex(box, slot))
                .ToArray();
            var preferredType = FindExistingSharedType(boxEntities);
            for (var slot = 0; slot < save.BoxSlotCount; slot++)
            {
                var entity = boxEntities[slot];
                var fingerprint = OrganizationStorageUtilities.Fingerprint(entity);
                fingerprints[(box, slot)] = fingerprint;
                if (entity.Species == 0)
                    continue;

                if (!TryGetType(entity.PersonalInfo.Type1, out var primary) ||
                    !TryGetType(entity.PersonalInfo.Type2, out var secondary))
                {
                    throw new NotSupportedException(
                        $"The Pokémon in box {box + 1}, slot {slot + 1} has unsupported type data.");
                }

                var stableId = $"{box:D3}:{slot:D2}:{fingerprint}";
                var reference = new PokemonReference(stableId, box, slot);
                pokemon.Add(new OrganizablePokemon(
                    reference,
                    entity.Species,
                    entity.Form,
                    entity.Gender,
                    entity.IsShiny,
                    primary,
                    secondary == primary ? null : secondary,
                    preferredType));
                snapshots.Add(stableId, entity.Clone());
            }
        }

        var boxes = selected
            .Select(box => new BoxState(box, boxNames[box], save.BoxSlotCount))
            .ToArray();
        var options = new TypeBoxOrganizerOptions(
            mode,
            renameBoxes,
            OrganizationStorageUtilities.GetMaximumBoxNameLength(save),
            GetLocalizedTypeNames());
        var plan = planner.CreatePlan(pokemon, boxes, options);
        return new TypeOrganizationSession(
            save,
            plan,
            selected,
            new ReadOnlyDictionary<string, PKM>(snapshots),
            new ReadOnlyDictionary<(int Box, int Slot), string>(fingerprints),
            new ReadOnlyDictionary<int, string>(boxNames));
    }

    public void Apply(TypeOrganizationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Plan.IsValid)
            throw new InvalidOperationException("An invalid organization plan cannot be applied.");

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
            new HashSet<(int Box, int Slot)>());
    }

    private SaveFile GetSupportedSave()
    {
        var save = saveFileProvider.SAV;
        if (!save.HasBox)
            throw new NotSupportedException("The loaded save does not provide storage boxes.");
        if (save.BoxSlotCount != TypeBoxOrganizationPlanner.BoxCapacity)
        {
            throw new NotSupportedException(
                $"Type-Optimized Box Allocation requires 30-slot boxes; this save uses {save.BoxSlotCount} slots per box.");
        }

        return save;
    }

    private static PokemonElementType? FindExistingSharedType(IReadOnlyList<PKM> entities)
    {
        HashSet<PokemonElementType>? shared = null;
        foreach (var entity in entities.Where(entity => entity.Species != 0))
        {
            if (!TryGetType(entity.PersonalInfo.Type1, out var primary) ||
                !TryGetType(entity.PersonalInfo.Type2, out var secondary))
            {
                return null;
            }

            var types = new HashSet<PokemonElementType> { primary };
            types.Add(secondary);
            if (shared is null)
                shared = types;
            else
                shared.IntersectWith(types);
            if (shared.Count == 0)
                return null;
        }

        return shared?.Order().FirstOrDefault();
    }

    private static bool TryGetType(byte value, out PokemonElementType type)
    {
        if (value <= (byte)PokemonElementType.Fairy)
        {
            type = (PokemonElementType)value;
            return true;
        }

        type = default;
        return false;
    }

    private static string GetBoxName(SaveFile save, int box) =>
        OrganizationStorageUtilities.GetBoxName(save, box);

    private static IReadOnlyDictionary<PokemonElementType, string> GetLocalizedTypeNames()
    {
        var localized = GameInfo.Strings.Types;
        return Enum.GetValues<PokemonElementType>()
            .ToDictionary(
                type => type,
                type => (int)type < localized.Count && !string.IsNullOrWhiteSpace(localized[(int)type])
                    ? localized[(int)type]
                    : TypeBoxNameGenerator.EnglishTypeNames[type]);
    }
}
