using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public sealed record GroupedPokemon
{
    public GroupedPokemon(
        PokemonReference reference,
        int species,
        int form,
        string speciesName,
        int level,
        uint experience,
        int evTotal,
        bool isShiny,
        int originGame,
        string originGameName,
        int gender,
        PokemonElementType? primaryType,
        string primaryTypeName,
        bool isEgg,
        bool isValid,
        bool isLegal,
        bool hasAllMoves)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegative(species);
        ArgumentOutOfRangeException.ThrowIfNegative(form);
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfNegative(evTotal);
        Reference = reference;
        Species = species;
        Form = form;
        SpeciesName = speciesName ?? string.Empty;
        Level = level;
        Experience = experience;
        EVTotal = evTotal;
        IsShiny = isShiny;
        OriginGame = originGame;
        OriginGameName = originGameName ?? string.Empty;
        Gender = gender;
        PrimaryType = primaryType;
        PrimaryTypeName = primaryTypeName ?? string.Empty;
        IsEgg = isEgg;
        IsValid = isValid;
        IsLegal = isLegal;
        HasAllMoves = hasAllMoves;
    }

    public PokemonReference Reference { get; }
    public int Species { get; }
    public int Form { get; }
    public string SpeciesName { get; }
    public int Level { get; }
    public uint Experience { get; }
    public int EVTotal { get; }
    public bool IsShiny { get; }
    public int OriginGame { get; }
    public string OriginGameName { get; }
    public int Gender { get; }
    public PokemonElementType? PrimaryType { get; }
    public string PrimaryTypeName { get; }
    public bool IsEgg { get; }
    public bool IsValid { get; }
    public bool IsLegal { get; }
    public bool HasAllMoves { get; }
}

public sealed record GroupedSlotAssignment(
    PokemonReference Pokemon,
    int TargetBoxIndex,
    int TargetSlotIndex,
    string GroupId);

public sealed record GroupedBoxAssignment(
    int TargetBoxIndex,
    string DisplayName,
    IReadOnlyList<PokemonReference> Pokemon,
    IReadOnlyList<string> GroupIds,
    BoxBackgroundTheme? BackgroundTheme);

public sealed record GroupCount(string GroupId, string DisplayName, int PokemonCount);

public sealed record GroupedOrganizationSummary(
    int IncludedPokemon,
    int FinalGroups,
    int RequiredBoxes,
    int AvailableBoxes,
    int UnusedSlots,
    int Eggs,
    int InvalidEntries);

public sealed class GroupedOrganizationPlan
{
    internal GroupedOrganizationPlan(
        string strategyName,
        string modeDescription,
        IEnumerable<GroupedSlotAssignment> assignments,
        IEnumerable<GroupedBoxAssignment> boxes,
        IEnumerable<GroupCount> groupCounts,
        IEnumerable<BoxRenameOperation> renames,
        IEnumerable<string> activeRules,
        IEnumerable<string> warnings,
        IEnumerable<string> errors,
        GroupedOrganizationSummary summary)
    {
        StrategyName = strategyName;
        ModeDescription = modeDescription;
        Assignments = ReadOnly(assignments);
        Boxes = ReadOnly(boxes);
        GroupCounts = ReadOnly(groupCounts);
        RenameOperations = ReadOnly(renames);
        ActiveRules = ReadOnly(activeRules);
        Warnings = ReadOnly(warnings);
        Errors = ReadOnly(errors);
        Summary = summary;
    }

    public string StrategyName { get; }
    public string ModeDescription { get; }
    public IReadOnlyList<GroupedSlotAssignment> Assignments { get; }
    public IReadOnlyList<GroupedBoxAssignment> Boxes { get; }
    public IReadOnlyList<GroupCount> GroupCounts { get; }
    public IReadOnlyList<BoxRenameOperation> RenameOperations { get; }
    public IReadOnlyList<string> ActiveRules { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }
    public GroupedOrganizationSummary Summary { get; }
    public bool IsValid => Errors.Count == 0;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

internal sealed record OrganizationGroup(
    string Id,
    string DisplayName,
    IReadOnlyList<GroupedPokemon> Pokemon,
    BoxBackgroundTheme? BackgroundTheme);

internal static class GroupedLayoutBuilder
{
    public const int BoxCapacity = 30;

    public static GroupedOrganizationPlan Build(
        string strategy,
        string mode,
        IReadOnlyList<OrganizationGroup> groups,
        IReadOnlyList<BoxState> boxes,
        bool startEachGroupInNewBox,
        bool renameBoxes,
        int maximumBoxNameLength,
        IReadOnlyList<string> activeRules,
        IReadOnlyList<string>? warnings = null)
    {
        var pokemon = groups.SelectMany(group => group.Pokemon).ToArray();
        var errors = Validate(pokemon, boxes);
        var requiredBoxes = startEachGroupInNewBox
            ? groups.Sum(group => DivideRoundUp(group.Pokemon.Count, BoxCapacity))
            : DivideRoundUp(pokemon.Length, BoxCapacity);
        if (requiredBoxes > boxes.Count)
        {
            errors.Add(
                $"The generated layout requires {requiredBoxes} boxes, but only {boxes.Count} selected boxes are available. " +
                (startEachGroupInNewBox ? "Starting each group in a new box reserves unused trailing slots." : string.Empty));
        }

        var summary = new GroupedOrganizationSummary(
            pokemon.Length,
            groups.Count,
            requiredBoxes,
            boxes.Count,
            Math.Max(0, (requiredBoxes * BoxCapacity) - pokemon.Length),
            pokemon.Count(item => item.IsEgg),
            pokemon.Count(item => !item.IsValid));
        if (errors.Count != 0)
        {
            return new GroupedOrganizationPlan(
                strategy, mode, [], [],
                groups.Select(group => new GroupCount(group.Id, group.DisplayName, group.Pokemon.Count)),
                [], activeRules, warnings ?? [], errors, summary);
        }

        var orderedBoxes = boxes.OrderBy(box => box.BoxIndex).ToArray();
        var layouts = startEachGroupInNewBox
            ? BuildSeparated(groups)
            : BuildCompact(groups);
        var boxAssignments = new List<GroupedBoxAssignment>(layouts.Count);
        var slotAssignments = new List<GroupedSlotAssignment>(pokemon.Length);
        for (var boxOffset = 0; boxOffset < layouts.Count; boxOffset++)
        {
            var targetBox = orderedBoxes[boxOffset].BoxIndex;
            var item = layouts[boxOffset];
            boxAssignments.Add(new GroupedBoxAssignment(
                targetBox,
                item.DisplayName,
                Array.AsReadOnly(item.Pokemon.Select(value => value.Pokemon.Reference).ToArray()),
                Array.AsReadOnly(item.GroupIds.ToArray()),
                item.BackgroundTheme));
            for (var slot = 0; slot < item.Pokemon.Count; slot++)
            {
                slotAssignments.Add(new GroupedSlotAssignment(
                    item.Pokemon[slot].Pokemon.Reference,
                    targetBox,
                    slot,
                    item.Pokemon[slot].GroupId));
            }
        }

        var renames = renameBoxes
            ? CreateRenames(boxAssignments, orderedBoxes.ToDictionary(box => box.BoxIndex), maximumBoxNameLength)
            : [];
        return new GroupedOrganizationPlan(
            strategy,
            mode,
            slotAssignments,
            boxAssignments,
            groups.Select(group => new GroupCount(group.Id, group.DisplayName, group.Pokemon.Count)),
            renames,
            activeRules,
            warnings ?? [],
            [],
            summary);
    }

    private static List<LogicalBox> BuildSeparated(IReadOnlyList<OrganizationGroup> groups)
    {
        var result = new List<LogicalBox>();
        foreach (var group in groups)
        {
            for (var offset = 0; offset < group.Pokemon.Count; offset += BoxCapacity)
            {
                result.Add(new LogicalBox(
                    group.DisplayName,
                    [group.Id],
                    group.Pokemon.Skip(offset).Take(BoxCapacity)
                        .Select(item => new GroupedItem(group.Id, item)).ToList(),
                    group.BackgroundTheme));
            }
        }
        return result;
    }

    private static List<LogicalBox> BuildCompact(IReadOnlyList<OrganizationGroup> groups)
    {
        var result = new List<LogicalBox>();
        foreach (var group in groups)
        {
            foreach (var pokemon in group.Pokemon)
            {
                if (result.Count == 0 || result[^1].Pokemon.Count == BoxCapacity)
                    result.Add(new LogicalBox(group.DisplayName, [], [], group.BackgroundTheme));
                var box = result[^1];
                box.Pokemon.Add(new GroupedItem(group.Id, pokemon));
                if (!box.GroupIds.Contains(group.Id, StringComparer.Ordinal))
                    box.GroupIds.Add(group.Id);
                if (box.GroupIds.Count > 1)
                {
                    box.DisplayName = "Custom";
                    box.BackgroundTheme = null;
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<BoxRenameOperation> CreateRenames(
        IReadOnlyList<GroupedBoxAssignment> assignments,
        IReadOnlyDictionary<int, BoxState> boxes,
        int maximumLength)
    {
        var counts = assignments.GroupBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<BoxRenameOperation>();
        foreach (var assignment in assignments)
        {
            var basis = Sanitize(assignment.DisplayName);
            var ordinal = ordinals.GetValueOrDefault(assignment.DisplayName) + 1;
            ordinals[assignment.DisplayName] = ordinal;
            var suffix = counts[assignment.DisplayName] > 1 ? $" {ordinal}" : string.Empty;
            var name = Fit(basis, suffix, maximumLength);
            var original = boxes[assignment.TargetBoxIndex].OriginalName;
            if (!string.Equals(original, name, StringComparison.Ordinal))
                result.Add(new BoxRenameOperation(assignment.TargetBoxIndex, original, name));
        }
        return result;
    }

    private static List<string> Validate(IReadOnlyList<GroupedPokemon> pokemon, IReadOnlyList<BoxState> boxes)
    {
        var errors = new List<string>();
        if (boxes.Any(box => box.Capacity != BoxCapacity))
            errors.Add("Organization currently requires storage boxes with exactly 30 slots.");
        if (boxes.Select(box => box.BoxIndex).Distinct().Count() != boxes.Count)
            errors.Add("The selected box list contains duplicate indices.");
        if (pokemon.Select(item => item.Reference.StableId).Distinct(StringComparer.Ordinal).Count() != pokemon.Count)
            errors.Add("The input contains duplicate stable Pokémon identities.");
        if (pokemon.Select(item => (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex)).Distinct().Count() != pokemon.Count)
            errors.Add("The input contains duplicate source slots.");
        if (pokemon.Any(item => boxes.All(box => box.BoxIndex != item.Reference.SourceBoxIndex)))
            errors.Add("Every included Pokémon must originate in a selected box.");
        return errors;
    }

    private static string Sanitize(string value)
    {
        var result = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return result.Length == 0 ? "Custom" : result;
    }

    private static string Fit(string basis, string suffix, int maximumLength)
    {
        if (suffix.Length >= maximumLength)
            return suffix[^maximumLength..];
        return string.Concat(basis.AsSpan(0, Math.Min(basis.Length, maximumLength - suffix.Length)), suffix);
    }

    private static int DivideRoundUp(int value, int divisor) =>
        value == 0 ? 0 : ((value - 1) / divisor) + 1;

    private sealed record GroupedItem(string GroupId, GroupedPokemon Pokemon);

    private sealed class LogicalBox(
        string displayName,
        List<string> groupIds,
        List<GroupedItem> pokemon,
        BoxBackgroundTheme? backgroundTheme)
    {
        public string DisplayName { get; set; } = displayName;
        public List<string> GroupIds { get; } = groupIds;
        public List<GroupedItem> Pokemon { get; } = pokemon;
        public BoxBackgroundTheme? BackgroundTheme { get; set; } = backgroundTheme;
    }
}
