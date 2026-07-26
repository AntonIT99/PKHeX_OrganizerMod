using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum LivingDexMode
{
    Species,
    Form,
    Shiny,
}

public enum LivingDexShinyScope
{
    Species,
    Form,
}

public enum LivingDexRepresentativePreference
{
    DefaultSafest,
    OldestObtained,
    Strongest,
}

public enum LivingDexEggHandling
{
    KeepInOverflow,
    ExcludeAndPreserve,
}

public enum LivingDexInvalidHandling
{
    KeepInOverflow,
    ExcludeAndPreserve,
}

public enum LivingDexOverflowOrder
{
    NationalDex,
    OriginalPosition,
    SpeciesThenQuality,
}

public enum LivingDexOverflowStart
{
    ImmediatelyAfterEntries,
    NextBoxBoundary,
}

public enum LivingDexStartPosition
{
    FirstSlotOfFirstSelectedBox,
}

public readonly record struct LivingDexEntryKey(
    int Species,
    int Form,
    bool RequiresShiny) : IComparable<LivingDexEntryKey>
{
    public int CompareTo(LivingDexEntryKey other)
    {
        var comparison = Species.CompareTo(other.Species);
        if (comparison != 0)
            return comparison;
        comparison = Form.CompareTo(other.Form);
        if (comparison != 0)
            return comparison;
        return RequiresShiny.CompareTo(other.RequiresShiny);
    }
}

public sealed record LivingDexEntryDefinition(
    LivingDexEntryKey Key,
    string SpeciesName,
    string? FormName,
    bool? IsShinyObtainable = null);

public sealed record LivingDexCandidate
{
    public LivingDexCandidate(
        PokemonReference reference,
        int species,
        int form,
        bool isShiny,
        bool isEgg,
        bool hasValidData,
        bool isLegal,
        bool isOwnedByCurrentTrainer,
        bool isFavoriteOrProtected,
        int level,
        int ivTotal,
        int evTotal,
        int ribbonOrMarkCount,
        DateOnly? obtainedDate)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(species);
        ArgumentOutOfRangeException.ThrowIfNegative(form);
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfNegative(ivTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(evTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(ribbonOrMarkCount);

        Reference = reference;
        Species = species;
        Form = form;
        IsShiny = isShiny;
        IsEgg = isEgg;
        HasValidData = hasValidData;
        IsLegal = isLegal;
        IsOwnedByCurrentTrainer = isOwnedByCurrentTrainer;
        IsFavoriteOrProtected = isFavoriteOrProtected;
        Level = level;
        IvTotal = ivTotal;
        EvTotal = evTotal;
        RibbonOrMarkCount = ribbonOrMarkCount;
        ObtainedDate = obtainedDate;
    }

    public PokemonReference Reference { get; }
    public int Species { get; }
    public int Form { get; }
    public bool IsShiny { get; }
    public bool IsEgg { get; }
    public bool HasValidData { get; }
    public bool IsLegal { get; }
    public bool IsOwnedByCurrentTrainer { get; }
    public bool IsFavoriteOrProtected { get; }
    public int Level { get; }
    public int IvTotal { get; }
    public int EvTotal { get; }
    public int RibbonOrMarkCount { get; }
    public DateOnly? ObtainedDate { get; }
}

public sealed record LivingDexOrganizerOptions
{
    public LivingDexOrganizerOptions(
        LivingDexMode mode,
        LivingDexShinyScope shinyScope,
        LivingDexRepresentativePreference representativePreference,
        LivingDexEggHandling eggHandling,
        LivingDexInvalidHandling invalidHandling,
        LivingDexOverflowOrder overflowOrder,
        LivingDexOverflowStart overflowStart,
        bool renameBoxes,
        int maximumBoxNameLength = 16,
        LivingDexStartPosition startPosition = LivingDexStartPosition.FirstSlotOfFirstSelectedBox)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(shinyScope))
            throw new ArgumentOutOfRangeException(nameof(shinyScope));
        if (!Enum.IsDefined(representativePreference))
            throw new ArgumentOutOfRangeException(nameof(representativePreference));
        if (!Enum.IsDefined(eggHandling))
            throw new ArgumentOutOfRangeException(nameof(eggHandling));
        if (!Enum.IsDefined(invalidHandling))
            throw new ArgumentOutOfRangeException(nameof(invalidHandling));
        if (!Enum.IsDefined(overflowOrder))
            throw new ArgumentOutOfRangeException(nameof(overflowOrder));
        if (!Enum.IsDefined(overflowStart))
            throw new ArgumentOutOfRangeException(nameof(overflowStart));
        if (!Enum.IsDefined(startPosition))
            throw new ArgumentOutOfRangeException(nameof(startPosition));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBoxNameLength);

        Mode = mode;
        ShinyScope = shinyScope;
        RepresentativePreference = representativePreference;
        EggHandling = eggHandling;
        InvalidHandling = invalidHandling;
        OverflowOrder = overflowOrder;
        OverflowStart = overflowStart;
        RenameBoxes = renameBoxes;
        MaximumBoxNameLength = maximumBoxNameLength;
        StartPosition = startPosition;
    }

    public LivingDexMode Mode { get; }
    public LivingDexShinyScope ShinyScope { get; }
    public LivingDexRepresentativePreference RepresentativePreference { get; }
    public LivingDexEggHandling EggHandling { get; }
    public LivingDexInvalidHandling InvalidHandling { get; }
    public LivingDexOverflowOrder OverflowOrder { get; }
    public LivingDexOverflowStart OverflowStart { get; }
    public bool RenameBoxes { get; }
    public int MaximumBoxNameLength { get; }
    public LivingDexStartPosition StartPosition { get; }
}

public sealed record MissingLivingDexEntry(LivingDexEntryDefinition Definition);

public sealed record LivingDexSlotAssignment(
    PokemonReference Pokemon,
    int TargetBoxIndex,
    int TargetSlotIndex,
    LivingDexEntryKey? Entry,
    bool IsOverflow);

public sealed record LivingDexBoxAssignment
{
    public LivingDexBoxAssignment(
        int targetBoxIndex,
        IEnumerable<PokemonReference> mainPokemon,
        IEnumerable<PokemonReference> overflowPokemon)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetBoxIndex);
        TargetBoxIndex = targetBoxIndex;
        MainPokemon = ReadOnly(mainPokemon);
        OverflowPokemon = ReadOnly(overflowPokemon);
    }

    public int TargetBoxIndex { get; }
    public IReadOnlyList<PokemonReference> MainPokemon { get; }
    public IReadOnlyList<PokemonReference> OverflowPokemon { get; }
    public int PokemonCount => MainPokemon.Count + OverflowPokemon.Count;
    public bool IsOverflowOnly => MainPokemon.Count == 0 && OverflowPokemon.Count != 0;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

public sealed record LivingDexSummary(
    int ExpectedEntries,
    int FilledEntries,
    int MissingEntries,
    double CompletionPercentage,
    int IncludedPokemon,
    int MainPokemon,
    int DuplicatePokemon,
    int OverflowPokemon,
    int PreservedPokemon,
    int MainBoxes,
    int OverflowBoxes,
    int RequiredBoxes,
    int SelectedBoxes,
    int AvailableSlots,
    int UnusedSelectedSlots);

public sealed class LivingDexOrganizationPlan
{
    internal LivingDexOrganizationPlan(
        LivingDexOrganizerOptions options,
        IEnumerable<LivingDexSlotAssignment> assignments,
        IEnumerable<LivingDexBoxAssignment> boxes,
        IEnumerable<PokemonReference> preservedPokemon,
        IEnumerable<BoxRenameOperation> renames,
        IEnumerable<MissingLivingDexEntry> missingEntries,
        IEnumerable<string> warnings,
        IEnumerable<string> errors,
        LivingDexSummary summary)
    {
        Options = options;
        Assignments = ReadOnly(assignments);
        Boxes = ReadOnly(boxes);
        PreservedPokemon = ReadOnly(preservedPokemon);
        RenameOperations = ReadOnly(renames);
        MissingEntries = ReadOnly(missingEntries);
        Warnings = ReadOnly(warnings);
        Errors = ReadOnly(errors);
        Summary = summary;
    }

    public LivingDexOrganizerOptions Options { get; }
    public IReadOnlyList<LivingDexSlotAssignment> Assignments { get; }
    public IReadOnlyList<LivingDexBoxAssignment> Boxes { get; }
    public IReadOnlyList<PokemonReference> PreservedPokemon { get; }
    public IReadOnlyList<BoxRenameOperation> RenameOperations { get; }
    public IReadOnlyList<MissingLivingDexEntry> MissingEntries { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }
    public LivingDexSummary Summary { get; }
    public bool IsValid => Errors.Count == 0;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}
