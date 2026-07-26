using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum ShinyDuplicateMode
{
    CombinedWithNonShiny,
    SeparateShinyGroup,
    IgnoreShiny,
}

public enum DuplicateSelectionCriterionType
{
    HighestLevel,
    PreferredOriginGame,
    PreferredGender,
}

public enum PokemonGenderPreference
{
    Male,
    Female,
    Genderless,
}

public sealed record DuplicateSelectionCriterion(
    DuplicateSelectionCriterionType Type,
    bool Enabled,
    int? PreferredOriginGame = null,
    PokemonGenderPreference? PreferredGender = null);

public sealed record DuplicateSpeciesOptions(
    ShinyDuplicateMode ShinyMode,
    IReadOnlyList<DuplicateSelectionCriterion> Criteria,
    IReadOnlySet<int> SelectedBoxIndices,
    IReadOnlySet<int>? SupportedOriginGameIds = null);

public sealed record DuplicateCandidate(
    PokemonReference Reference,
    int Species,
    int Form,
    bool IsShiny,
    int Level,
    int OriginGameId,
    PokemonGenderPreference Gender,
    bool IsEgg,
    bool IsValid);

public readonly record struct DuplicateGroupKey(int Species, bool? IsShiny);

public sealed record DuplicateRemovalDecision(
    DuplicateGroupKey Key,
    DuplicateCandidate Kept,
    IReadOnlyList<DuplicateCandidate> Removed,
    IReadOnlyList<string> Reasons)
{
    public int CandidateCount => Removed.Count + 1;
}

public sealed record DuplicateRemovalSummary(
    int SelectedBoxes,
    int PokemonScanned,
    int PokemonAnalyzed,
    int UniqueSpeciesRepresented,
    int DuplicateGroups,
    int KeptRepresentatives,
    int RemovalCandidates,
    int EggsIgnored,
    int InvalidEntriesIgnored,
    int ShinyPokemonIgnored);

public sealed class SpeciesDuplicateRemovalPlan
{
    internal SpeciesDuplicateRemovalPlan(
        DuplicateSpeciesOptions options,
        IEnumerable<DuplicateRemovalDecision> decisions,
        DuplicateRemovalSummary summary,
        IEnumerable<string> errors)
    {
        Options = options;
        Decisions = Array.AsReadOnly(decisions.ToArray());
        Summary = summary;
        Errors = Array.AsReadOnly(errors.ToArray());
        RemovalCandidates = Array.AsReadOnly(
            Decisions.SelectMany(item => item.Removed)
                .OrderBy(item => item.Reference.SourceBoxIndex)
                .ThenBy(item => item.Reference.SourceSlotIndex)
                .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
                .ToArray());
    }

    public DuplicateSpeciesOptions Options { get; }
    public ReadOnlyCollection<DuplicateRemovalDecision> Decisions { get; }
    public ReadOnlyCollection<DuplicateCandidate> RemovalCandidates { get; }
    public DuplicateRemovalSummary Summary { get; }
    public ReadOnlyCollection<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
}

public static class DuplicateCriterionList
{
    public static IReadOnlyList<T> Move<T>(IReadOnlyList<T> items, int index, int offset)
    {
        ArgumentNullException.ThrowIfNull(items);
        if ((uint)index >= (uint)items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (offset is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(offset));
        var target = index + offset;
        if ((uint)target >= (uint)items.Count)
            return Array.AsReadOnly(items.ToArray());
        var result = items.ToArray();
        (result[index], result[target]) = (result[target], result[index]);
        return Array.AsReadOnly(result);
    }
}
