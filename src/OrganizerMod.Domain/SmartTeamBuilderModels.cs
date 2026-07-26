using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum TeamTypeMatchingMode { HasAnySelectedType, HasAllSelectedTypes, ExactTypeCombination }
public enum TeamEligibilityRuleType { RequiredTypes, RequiredOriginGame, RequiredSpeciesGeneration, LegendaryOrMythicalOnly, ShinyOnly }
public enum TeamPreferenceCriterionType { HighestLevelAndExperience, PreferredTypes, PreferredOriginGame, PreferredSpeciesGeneration, PreferLegendaryOrMythical, PreferShiny }
public enum TeamPartyOrder { PreferenceOrder, PreserveCurrentTeamOrder, LevelDescending }

public sealed record TeamBuilderCandidate(
    string StableId,
    PokemonStorageLocation OriginalLocation,
    int Species,
    int Form,
    string DisplayName,
    int Level,
    ulong Experience,
    PokemonElementType PrimaryType,
    PokemonElementType? SecondaryType,
    int OriginGame,
    string OriginGameName,
    int SpeciesGeneration,
    bool IsLegendaryOrMythical,
    bool IsShiny,
    bool IsEgg,
    bool IsValid)
{
    public IReadOnlyList<PokemonElementType> Types =>
        SecondaryType is { } secondary && secondary != PrimaryType
            ? [PrimaryType, secondary]
            : [PrimaryType];
}

public sealed record TeamEligibilityRule(
    TeamEligibilityRuleType Type,
    bool Enabled,
    IReadOnlyList<PokemonElementType>? Types = null,
    TeamTypeMatchingMode TypeMatching = TeamTypeMatchingMode.HasAnySelectedType,
    int? OriginGame = null,
    int? SpeciesGeneration = null);

public sealed record TeamPreferenceCriterion(
    TeamPreferenceCriterionType Type,
    bool Enabled,
    IReadOnlyList<PokemonElementType>? Types = null,
    int? OriginGame = null,
    int? SpeciesGeneration = null);

public sealed record TeamBuilderOptions(
    int RequestedTeamSize,
    int MaximumTeamSize,
    IReadOnlyList<TeamEligibilityRule> EligibilityRules,
    IReadOnlyList<TeamPreferenceCriterion> PreferenceCriteria,
    bool PreferDifferentSpecies,
    TeamPartyOrder PartyOrder,
    IReadOnlySet<int> SelectedBoxIndices,
    bool AllowEggs = false,
    bool AllowSmallerTeam = false);

public sealed record TeamSelectionDecision(
    string StableId,
    int FinalTeamSlot,
    IReadOnlyList<string> Reasons);

public sealed record TeamBoxAssignment(string StableId, int BoxIndex, int SlotIndex);

public sealed record TeamLocationChange(
    string StableId,
    PokemonStorageLocation Source,
    PokemonStorageLocation Destination);

public sealed record TeamBuilderSummary(
    int CandidateBoxes,
    int CandidatePokemon,
    int EligiblePokemon,
    int RequestedTeamSize,
    int SelectedTeamSize,
    int RetainedTeamPokemon,
    int MovedFromBoxesToTeam,
    int MovedFromTeamToBoxes,
    int UnchangedBoxPokemon,
    int ExcludedInvalid,
    int ExcludedEggs);

public sealed class TeamExchangePlan
{
    public TeamExchangePlan(
        TeamBuilderOptions options,
        IEnumerable<TeamSelectionDecision> selectedTeam,
        IEnumerable<TeamBoxAssignment> finalBoxAssignments,
        IEnumerable<TeamLocationChange> locationChanges,
        TeamBuilderSummary summary,
        IEnumerable<string> warnings,
        IEnumerable<string> errors,
        IReadOnlyDictionary<string, int> sequentialExclusionCounts)
    {
        Options = options;
        SelectedTeam = Array.AsReadOnly(selectedTeam.ToArray());
        FinalBoxAssignments = Array.AsReadOnly(finalBoxAssignments.ToArray());
        LocationChanges = Array.AsReadOnly(locationChanges.ToArray());
        Summary = summary;
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        SequentialExclusionCounts = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(sequentialExclusionCounts, StringComparer.Ordinal));
    }

    public TeamBuilderOptions Options { get; }
    public IReadOnlyList<TeamSelectionDecision> SelectedTeam { get; }
    public IReadOnlyList<TeamBoxAssignment> FinalBoxAssignments { get; }
    public IReadOnlyList<TeamLocationChange> LocationChanges { get; }
    public TeamBuilderSummary Summary { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyDictionary<string, int> SequentialExclusionCounts { get; }
    public bool IsValid => Errors.Count == 0;
}
