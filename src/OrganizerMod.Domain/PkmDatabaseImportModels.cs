using System.Collections.ObjectModel;

namespace OrganizerMod.Domain;

public enum SamePidImportMode { ImportAdditionally, ReplaceWhenMoreAdvanced, DoNotImport }
public enum SameSpeciesShinyImportMode { ImportAdditionally, BestDatabaseRepresentativeReplaceWhenBetter, DoNotImportWhenExisting }
public enum SpeciesShinyGroupingMode { Separate, Combined }
public enum LegalityFilterMode { Regardless, OnlyLegal }
public enum DatabaseDecisionKind { NewImport, Replacement, Skipped }
public enum DatabaseDecisionRule { Filter, SamePid, SameSpeciesAndShiny, NoConflict, Compatibility, Capacity }
public enum ExistingPokemonArea { Box, Team, Pension }

public sealed record PkmDatabaseFilterOptions(
    LegalityFilterMode Legality,
    int? OriginGame,
    int? MinimumLevel,
    PokemonGenderPreference? Gender,
    bool? IsShiny = null);

public sealed record PkmDatabaseImportOptions(
    SamePidImportMode SamePidMode,
    SameSpeciesShinyImportMode SameSpeciesShinyMode,
    PkmDatabaseFilterOptions Filters,
    IReadOnlySet<int> SelectedBoxIndices,
    bool IncludeTeamInPidComparison = false,
    bool IncludePensionInPidComparison = false,
    SpeciesShinyGroupingMode SpeciesShinyGrouping = SpeciesShinyGroupingMode.Separate,
    bool AllowTeamReplacements = false,
    bool UseTeamSlotsForNewImports = false);

public sealed record DatabasePokemonCandidate(
    string StableId,
    string RelativeSourcePath,
    uint Pid,
    int Species,
    int Form,
    bool IsShiny,
    int Level,
    ulong Experience,
    int OriginGameId,
    PokemonGenderPreference Gender,
    bool? IsLegal,
    bool IsCompatible);

public sealed record ExistingSavePokemon(
    string StableId,
    uint Pid,
    int Species,
    int Form,
    bool IsShiny,
    int Level,
    ulong Experience,
    int OriginGameId,
    PokemonGenderPreference Gender,
    int BoxIndex,
    int SlotIndex,
    ExistingPokemonArea Area = ExistingPokemonArea.Box,
    int FacilityIndex = 0)
{
    public bool CanReplace => Area == ExistingPokemonArea.Box;
}

public readonly record struct EmptySaveSlot(
    int BoxIndex,
    int SlotIndex,
    ExistingPokemonArea Area = ExistingPokemonArea.Box);

public sealed record DatabaseImportDecision(
    DatabasePokemonCandidate Candidate,
    DatabaseDecisionKind Kind,
    DatabaseDecisionRule Rule,
    string Reason,
    IReadOnlyList<ExistingSavePokemon> Matches,
    EmptySaveSlot? ImportDestination,
    ExistingSavePokemon? ReplacementTarget);

public sealed record PokemonImportOperation(
    DatabasePokemonCandidate Candidate,
    EmptySaveSlot Destination,
    string Reason);

public sealed record PokemonReplacementOperation(
    DatabasePokemonCandidate Candidate,
    ExistingSavePokemon Existing,
    string Reason);

public sealed record DatabaseFilterStatistics(
    int ExcludedByLegality,
    int ExcludedByOrigin,
    int ExcludedByMinimumLevel,
    int ExcludedByGender,
    int ExcludedByShiny);

public sealed record DatabaseImportSummary(
    int FilesScanned,
    int LoadedPokemon,
    int EligibleAfterFilters,
    int ExistingPokemonCompared,
    int NewImports,
    int Replacements,
    int Skipped,
    int EmptyDestinationSlots,
    int RemainingFreeSlots,
    int UnreadableFiles,
    int IncompatiblePokemon,
    DatabaseFilterStatistics Filters);

public sealed class DatabaseImportPlan
{
    internal DatabaseImportPlan(
        PkmDatabaseImportOptions options,
        IEnumerable<DatabaseImportDecision> decisions,
        DatabaseImportSummary summary,
        IEnumerable<string> warnings,
        IEnumerable<string> errors)
    {
        Options = options;
        Decisions = Array.AsReadOnly(decisions.ToArray());
        Imports = Array.AsReadOnly(Decisions.Where(x => x.Kind == DatabaseDecisionKind.NewImport && x.ImportDestination is not null)
            .Select(x => new PokemonImportOperation(x.Candidate, x.ImportDestination!.Value, x.Reason)).ToArray());
        Replacements = Array.AsReadOnly(Decisions.Where(x => x.Kind == DatabaseDecisionKind.Replacement && x.ReplacementTarget is not null)
            .Select(x => new PokemonReplacementOperation(x.Candidate, x.ReplacementTarget!, x.Reason)).ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        Errors = Array.AsReadOnly(errors.ToArray());
        Summary = summary;
    }
    public PkmDatabaseImportOptions Options { get; }
    public ReadOnlyCollection<DatabaseImportDecision> Decisions { get; }
    public ReadOnlyCollection<PokemonImportOperation> Imports { get; }
    public ReadOnlyCollection<PokemonReplacementOperation> Replacements { get; }
    public DatabaseImportSummary Summary { get; }
    public ReadOnlyCollection<string> Warnings { get; }
    public ReadOnlyCollection<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;
}
