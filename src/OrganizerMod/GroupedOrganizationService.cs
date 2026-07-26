using System.Collections.ObjectModel;
using OrganizerMod.Domain;
using PKHeX.Core;

namespace OrganizerMod;

internal sealed class GroupedOrganizationSession(
    SaveFile save,
    GroupedOrganizationPlan plan,
    IReadOnlyList<int> selectedBoxes,
    IReadOnlyDictionary<string, PKM> pokemonSnapshots,
    IReadOnlyDictionary<(int Box, int Slot), string> slotFingerprints,
    IReadOnlyDictionary<int, string> boxNames,
    bool assignMatchingBackgrounds,
    IReadOnlyList<BoxBackgroundPreview> backgroundPreviews,
    IReadOnlyList<BoxBackgroundChangeOperation> backgroundChanges,
    IReadOnlyDictionary<int, int> originalBackgrounds)
{
    public SaveFile Save { get; } = save;
    public GroupedOrganizationPlan Plan { get; } = plan;
    public IReadOnlyList<int> SelectedBoxes { get; } = selectedBoxes;
    public IReadOnlyDictionary<string, PKM> PokemonSnapshots { get; } = pokemonSnapshots;
    public IReadOnlyDictionary<(int Box, int Slot), string> SlotFingerprints { get; } = slotFingerprints;
    public IReadOnlyDictionary<int, string> BoxNames { get; } = boxNames;
    public bool AssignMatchingBackgrounds { get; } = assignMatchingBackgrounds;
    public IReadOnlyList<BoxBackgroundPreview> BackgroundPreviews { get; } = backgroundPreviews;
    public IReadOnlyList<BoxBackgroundChangeOperation> BackgroundChanges { get; } = backgroundChanges;
    public IReadOnlyDictionary<int, int> OriginalBackgrounds { get; } = originalBackgrounds;
}

internal sealed class GroupedOrganizationService(ISaveFileProvider saveFileProvider)
{
    private readonly CompetitiveOrganizationPlanner competitivePlanner = new();
    private readonly CustomOrganizationPlanner customPlanner = new();

    public bool CanRenameBoxes => saveFileProvider.SAV is IBoxDetailName;
    public bool CanAssignBackgrounds => new BoxBackgroundCatalog(saveFileProvider.SAV).CanAssign;

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

    public GroupedOrganizationSession CreateCompetitivePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        CompetitiveOrganizerOptions options) =>
        CreatePlan(selectedBoxIndices, options.RequireLegal,
            (pokemon, boxes) => competitivePlanner.CreatePlan(pokemon, boxes, options),
            options.AssignMatchingBackgrounds);

    public GroupedOrganizationSession CreateCustomPlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        CustomOrganizerOptions options) =>
        CreatePlan(selectedBoxIndices, requireLegality: false,
            (pokemon, boxes) => customPlanner.CreatePlan(pokemon, boxes, options),
            options.AssignMatchingBackgrounds);

    public void Apply(GroupedOrganizationSession session)
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
            session.Plan.Assignments.Select(item => (item.Pokemon, item.TargetBoxIndex, item.TargetSlotIndex)),
            session.Plan.RenameOperations,
            new HashSet<(int Box, int Slot)>(),
            session.OriginalBackgrounds,
            session.BackgroundChanges);
    }

    private GroupedOrganizationSession CreatePlan(
        IReadOnlyCollection<int> selectedBoxIndices,
        bool requireLegality,
        Func<IReadOnlyList<GroupedPokemon>, IReadOnlyList<BoxState>, GroupedOrganizationPlan> create,
        bool assignMatchingBackgrounds)
    {
        ArgumentNullException.ThrowIfNull(selectedBoxIndices);
        var save = GetSupportedSave();
        if (selectedBoxIndices.Count == 0)
            throw new InvalidOperationException("Select at least one available box to organize.");
        var choices = GetBoxSelection().ToDictionary(item => item.BoxIndex);
        var selected = selectedBoxIndices.Distinct().Order().ToArray();
        foreach (var box in selected)
        {
            if (!choices.TryGetValue(box, out var choice))
                throw new ArgumentOutOfRangeException(nameof(selectedBoxIndices), $"Box {box + 1} does not exist.");
            if (!choice.IsAvailable)
                throw new InvalidOperationException($"Box {box + 1} is unavailable because it {choice.UnavailableReason}.");
        }

        var snapshots = new Dictionary<string, PKM>(StringComparer.Ordinal);
        var fingerprints = new Dictionary<(int Box, int Slot), string>();
        var boxNames = selected.ToDictionary(box => box, box => OrganizationStorageUtilities.GetBoxName(save, box));
        var pokemon = new List<GroupedPokemon>();
        var speciesNames = GameInfo.Strings.Species;
        var gameNames = GameInfo.Strings.gamelist;
        var typeNames = GameInfo.Strings.Types;
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
                var isLegal = false;
                if (requireLegality)
                {
                    try { isLegal = new LegalityAnalysis(entity, save.Personal).Valid; }
                    catch { /* Invalid data remains non-legal neutral metadata. */ }
                }
                var typeValue = entity.PersonalInfo.Type1;
                var primaryType = typeValue <= (byte)PokemonElementType.Fairy
                    ? (PokemonElementType?)typeValue
                    : null;
                var origin = (int)entity.Version;
                pokemon.Add(new GroupedPokemon(
                    new PokemonReference(stableId, box, slot),
                    entity.Species,
                    entity.Form,
                    entity.Species < speciesNames.Count ? speciesNames[entity.Species] : $"Species {entity.Species}",
                    entity.CurrentLevel,
                    entity.EXP,
                    entity.EVTotal,
                    entity.IsShiny,
                    origin,
                    origin >= 0 && origin < gameNames.Length ? gameNames[origin] : "Unknown Origin",
                    entity.Gender,
                    primaryType,
                    primaryType is { } type && (int)type < typeNames.Count ? typeNames[(int)type] : "Unknown Type",
                    entity.IsEgg,
                    entity.Valid && entity.Species < speciesNames.Count && entity.CurrentLevel is >= 1 and <= 100,
                    isLegal,
                    entity.Move1 != 0 && entity.Move2 != 0 && entity.Move3 != 0 && entity.Move4 != 0));
                snapshots.Add(stableId, entity.Clone());
            }
        }

        var boxes = selected.Select(box => new BoxState(box, boxNames[box], save.BoxSlotCount)).ToArray();
        var plan = create(pokemon, boxes);
        var resolved = ResolveBackgrounds(plan, new BoxBackgroundCatalog(save), assignMatchingBackgrounds);
        return new GroupedOrganizationSession(
            save,
            plan,
            selected,
            new ReadOnlyDictionary<string, PKM>(snapshots),
            new ReadOnlyDictionary<(int Box, int Slot), string>(fingerprints),
            new ReadOnlyDictionary<int, string>(boxNames),
            assignMatchingBackgrounds,
            resolved.Previews,
            resolved.Changes,
            resolved.Originals);
    }

    private static (
        IReadOnlyList<BoxBackgroundPreview> Previews,
        IReadOnlyList<BoxBackgroundChangeOperation> Changes,
        IReadOnlyDictionary<int, int> Originals) ResolveBackgrounds(
        GroupedOrganizationPlan plan,
        BoxBackgroundCatalog catalog,
        bool enabled)
    {
        if (!enabled || !catalog.CanAssign)
            return ([], [], new ReadOnlyDictionary<int, int>(new Dictionary<int, int>()));
        var previews = new List<BoxBackgroundPreview>();
        var changes = new List<BoxBackgroundChangeOperation>();
        var originals = new Dictionary<int, int>();
        foreach (var box in plan.Boxes.Where(item => item.BackgroundTheme is not null))
        {
            var theme = box.BackgroundTheme!.Value;
            if (!catalog.TryResolveTheme(theme, out var resolved))
                continue;
            var original = catalog.GetCurrentWallpaper(box.TargetBoxIndex);
            originals[box.TargetBoxIndex] = original;
            var originalName = catalog.GetDisplayName(original);
            var changed = original != resolved.WallpaperId;
            previews.Add(new BoxBackgroundPreview(
                box.TargetBoxIndex, null, false, theme, BackgroundThemeChoice.Primary,
                original, resolved.WallpaperId, originalName, resolved.DisplayName, changed, false, null));
            if (changed)
            {
                changes.Add(new BoxBackgroundChangeOperation(
                    box.TargetBoxIndex, theme, BackgroundThemeChoice.Primary,
                    original, resolved.WallpaperId, originalName, resolved.DisplayName));
            }
        }
        return (Array.AsReadOnly(previews.ToArray()), Array.AsReadOnly(changes.ToArray()),
            new ReadOnlyDictionary<int, int>(originals));
    }

    private SaveFile GetSupportedSave()
    {
        var save = saveFileProvider.SAV;
        if (!save.HasBox)
            throw new NotSupportedException("The loaded save does not provide storage boxes.");
        if (save.BoxSlotCount != 30)
            throw new NotSupportedException($"This strategy requires 30-slot boxes; the save uses {save.BoxSlotCount}.");
        return save;
    }
}
