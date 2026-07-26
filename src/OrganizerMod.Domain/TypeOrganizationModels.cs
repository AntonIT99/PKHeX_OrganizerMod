using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum PokemonElementType
{
    Normal = 0,
    Fighting = 1,
    Flying = 2,
    Poison = 3,
    Ground = 4,
    Rock = 5,
    Bug = 6,
    Ghost = 7,
    Steel = 8,
    Fire = 9,
    Water = 10,
    Grass = 11,
    Electric = 12,
    Psychic = 13,
    Ice = 14,
    Dragon = 15,
    Dark = 16,
    Fairy = 17,
}

public enum TypeBoxLayoutMode
{
    Compact,
    ExpandedByType,
}

public sealed record PokemonReference
{
    public PokemonReference(string stableId, int sourceBoxIndex, int sourceSlotIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceBoxIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceSlotIndex);
        StableId = stableId;
        SourceBoxIndex = sourceBoxIndex;
        SourceSlotIndex = sourceSlotIndex;
    }

    public string StableId { get; }
    public int SourceBoxIndex { get; }
    public int SourceSlotIndex { get; }
}

public sealed record OrganizablePokemon
{
    public OrganizablePokemon(
        PokemonReference reference,
        int species,
        int form,
        int gender,
        bool isShiny,
        PokemonElementType primaryType,
        PokemonElementType? secondaryType = null,
        PokemonElementType? preferredType = null,
        bool isLegendary = false)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(species);
        ArgumentOutOfRangeException.ThrowIfNegative(form);
        ArgumentOutOfRangeException.ThrowIfNegative(gender);
        ValidateType(primaryType, nameof(primaryType));
        if (secondaryType is { } secondary)
            ValidateType(secondary, nameof(secondaryType));
        if (preferredType is { } preferred)
            ValidateType(preferred, nameof(preferredType));

        Reference = reference;
        Species = species;
        Form = form;
        Gender = gender;
        IsShiny = isShiny;
        PrimaryType = primaryType;
        SecondaryType = secondaryType == primaryType ? null : secondaryType;
        PreferredType = preferredType is not null && CanHaveType(primaryType, SecondaryType, preferredType.Value)
            ? preferredType
            : null;
        IsLegendary = isLegendary;
    }

    public PokemonReference Reference { get; }
    public int Species { get; }
    public int Form { get; }
    public int Gender { get; }
    public bool IsShiny { get; }
    public PokemonElementType PrimaryType { get; }
    public PokemonElementType? SecondaryType { get; }
    public PokemonElementType? PreferredType { get; }
    public bool IsLegendary { get; }
    public bool IsDualType => SecondaryType is not null;

    public bool CanBeAssignedTo(PokemonElementType type) =>
        type == PrimaryType || type == SecondaryType;

    private static bool CanHaveType(
        PokemonElementType primary,
        PokemonElementType? secondary,
        PokemonElementType candidate) =>
        candidate == primary || candidate == secondary;

    private static void ValidateType(PokemonElementType type, string parameterName)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(parameterName, type, "Unknown Pokémon type.");
    }
}

public sealed record BoxState
{
    public BoxState(int boxIndex, string originalName, int capacity = 30)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boxIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        BoxIndex = boxIndex;
        OriginalName = originalName ?? string.Empty;
        Capacity = capacity;
    }

    public int BoxIndex { get; }
    public string OriginalName { get; }
    public int Capacity { get; }
}

public sealed record TypeBoxOrganizerOptions
{
    public TypeBoxOrganizerOptions(
        TypeBoxLayoutMode layoutMode,
        bool renameBoxes,
        int maximumBoxNameLength = 16,
        IReadOnlyDictionary<PokemonElementType, string>? typeNames = null,
        bool assignMatchingBackgrounds = false,
        bool rotateAlternativeBackgrounds = false,
        IReadOnlySet<BoxBackgroundTheme>? supportedBackgroundThemes = null,
        bool groupLegendaries = false)
    {
        if (!Enum.IsDefined(layoutMode))
            throw new ArgumentOutOfRangeException(nameof(layoutMode));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBoxNameLength);

        LayoutMode = layoutMode;
        RenameBoxes = renameBoxes;
        MaximumBoxNameLength = maximumBoxNameLength;
        TypeNames = new ReadOnlyDictionary<PokemonElementType, string>(
            new Dictionary<PokemonElementType, string>(
                typeNames ?? TypeBoxNameGenerator.EnglishTypeNames));
        AssignMatchingBackgrounds = assignMatchingBackgrounds;
        RotateAlternativeBackgrounds = assignMatchingBackgrounds && rotateAlternativeBackgrounds;
        SupportedBackgroundThemes = new HashSet<BoxBackgroundTheme>(
            supportedBackgroundThemes ?? new HashSet<BoxBackgroundTheme>());
        GroupLegendaries = groupLegendaries;
    }

    public TypeBoxLayoutMode LayoutMode { get; }
    public bool RenameBoxes { get; }
    public int MaximumBoxNameLength { get; }
    public IReadOnlyDictionary<PokemonElementType, string> TypeNames { get; }
    public bool AssignMatchingBackgrounds { get; }
    public bool RotateAlternativeBackgrounds { get; }
    public IReadOnlySet<BoxBackgroundTheme> SupportedBackgroundThemes { get; }
    public bool GroupLegendaries { get; }
}

public sealed record TypeSlotAssignment(
    PokemonReference Pokemon,
    int TargetBoxIndex,
    int TargetSlotIndex,
    PokemonElementType? AssignedType,
    bool IsMixed,
    bool IsLegendary = false);

public sealed record TypeBoxAssignment
{
    public TypeBoxAssignment(
        int targetBoxIndex,
        PokemonElementType? sharedType,
        IEnumerable<PokemonReference> pokemon,
        bool isMixed,
        bool isLegendary = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetBoxIndex);
        ArgumentNullException.ThrowIfNull(pokemon);
        if ((isMixed || isLegendary) && sharedType is not null)
            throw new ArgumentException("A mixed or Legendary box cannot declare a shared type.", nameof(sharedType));
        if (!isMixed && !isLegendary && sharedType is null)
            throw new ArgumentException("A type box must declare its shared type.", nameof(sharedType));
        if (isMixed && isLegendary)
            throw new ArgumentException("A box cannot be both mixed and Legendary.", nameof(isLegendary));

        TargetBoxIndex = targetBoxIndex;
        SharedType = sharedType;
        Pokemon = new ReadOnlyCollection<PokemonReference>(pokemon.ToArray());
        IsMixed = isMixed;
        IsLegendary = isLegendary;
    }

    public int TargetBoxIndex { get; }
    public PokemonElementType? SharedType { get; }
    public IReadOnlyList<PokemonReference> Pokemon { get; }
    public bool IsMixed { get; }
    public bool IsLegendary { get; }
}

public sealed record BoxRenameOperation(
    int BoxIndex,
    string OriginalName,
    string NewName);

public sealed record TypeOrganizationSummary(
    int FullTypeBoxes,
    int PartialTypeBoxes,
    int MixedBoxes,
    int PokemonInTypeBoxes,
    int PokemonInMixedBoxes,
    int UsedBoxes,
    int UnusedSlots,
    int LegendaryBoxes = 0,
    int LegendaryPokemon = 0);

public readonly record struct AllocationScore(
    int Primary,
    int Secondary,
    int Tertiary,
    int Quaternary,
    int Quinary,
    int Senary) : IComparable<AllocationScore>
{
    public int CompareTo(AllocationScore other)
    {
        var values = new[] { Primary, Secondary, Tertiary, Quaternary, Quinary, Senary };
        var otherValues = new[] { other.Primary, other.Secondary, other.Tertiary, other.Quaternary, other.Quinary, other.Senary };
        for (var index = 0; index < values.Length; index++)
        {
            var comparison = values[index].CompareTo(otherValues[index]);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }
}

public sealed class TypeOrganizationPlan
{
    internal TypeOrganizationPlan(
        TypeBoxLayoutMode layoutMode,
        IEnumerable<TypeSlotAssignment> assignments,
        IEnumerable<TypeBoxAssignment> boxes,
        IEnumerable<BoxRenameOperation> renames,
        IEnumerable<PlannedBoxBackgroundTheme> backgroundThemes,
        IEnumerable<string> warnings,
        IEnumerable<string> errors,
        TypeOrganizationSummary summary,
        int usableBoxCount,
        int pokemonCount)
    {
        LayoutMode = layoutMode;
        Assignments = ReadOnly(assignments);
        Boxes = ReadOnly(boxes);
        RenameOperations = ReadOnly(renames);
        BackgroundThemes = ReadOnly(backgroundThemes);
        Warnings = ReadOnly(warnings);
        Errors = ReadOnly(errors);
        Summary = summary;
        UsableBoxCount = usableBoxCount;
        PokemonCount = pokemonCount;
    }

    public TypeBoxLayoutMode LayoutMode { get; }
    public IReadOnlyList<TypeSlotAssignment> Assignments { get; }
    public IReadOnlyList<TypeBoxAssignment> Boxes { get; }
    public IReadOnlyList<BoxRenameOperation> RenameOperations { get; }
    public IReadOnlyList<PlannedBoxBackgroundTheme> BackgroundThemes { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }
    public TypeOrganizationSummary Summary { get; }
    public int UsableBoxCount { get; }
    public int PokemonCount { get; }
    public bool IsValid => Errors.Count == 0;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}
