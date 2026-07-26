namespace OrganizerMod.Domain;

public sealed class PkmDatabaseImportPlanner
{
    public DatabaseImportPlan CreatePlan(
        IReadOnlyList<DatabasePokemonCandidate> database,
        IReadOnlyList<ExistingSavePokemon> existing,
        IReadOnlyList<EmptySaveSlot> emptySlots,
        PkmDatabaseImportOptions options,
        int filesScanned = 0,
        int unreadableFiles = 0,
        IReadOnlyList<string>? scanWarnings = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(emptySlots);
        ArgumentNullException.ThrowIfNull(options);
        var errors = Validate(options);
        var warnings = scanWarnings?.ToList() ?? [];
        var ordered = database.OrderBy(x => x.RelativeSourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        var selectedExisting = existing.Where(x => x.Area == ExistingPokemonArea.Box && options.SelectedBoxIndices.Contains(x.BoxIndex))
            .OrderBy(x => x.BoxIndex).ThenBy(x => x.SlotIndex).ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        var pidComparison = selectedExisting
            .Concat(existing.Where(x =>
                (x.Area == ExistingPokemonArea.Team && options.IncludeTeamInPidComparison) ||
                (x.Area == ExistingPokemonArea.Pension && options.IncludePensionInPidComparison)))
            .DistinctBy(x => x.StableId)
            .OrderBy(x => x.Area).ThenBy(x => x.FacilityIndex).ThenBy(x => x.BoxIndex).ThenBy(x => x.SlotIndex)
            .ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        var slots = emptySlots.Where(x =>
                x.Area == ExistingPokemonArea.Team && options.UseTeamSlotsForNewImports ||
                x.Area == ExistingPokemonArea.Box && options.SelectedBoxIndices.Contains(x.BoxIndex))
            .OrderBy(x => x.Area == ExistingPokemonArea.Team ? 0 : 1)
            .ThenBy(x => x.BoxIndex).ThenBy(x => x.SlotIndex).Distinct().ToArray();

        var (eligible, filtered, stats) = Filter(ordered, options.Filters);
        var decisions = new List<DatabaseImportDecision>(ordered.Length);
        decisions.AddRange(filtered);
        var compatible = new List<DatabasePokemonCandidate>(eligible.Count);
        foreach (var candidate in eligible)
        {
            if (candidate.IsCompatible)
                compatible.Add(candidate);
            else
                decisions.Add(Skip(candidate, DatabaseDecisionRule.Compatibility, "Cannot be represented by the loaded save format."));
        }
        var batchEligible = ReduceSamePidSpeciesBatch(compatible, options.SamePidMode, decisions);
        var pidIndex = pidComparison.GroupBy(x => x.Pid).ToDictionary(x => x.Key, x => x.ToArray());
        var speciesIndex = selectedExisting.GroupBy(x => SpeciesKey(x.Species, x.IsShiny, options.SpeciesShinyGrouping))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var unresolved = new List<DatabasePokemonCandidate>();
        var replacementTargets = new HashSet<(ExistingPokemonArea Area, int Facility, int Box, int Slot)>();

        foreach (var candidate in batchEligible)
        {
            if (!pidIndex.TryGetValue(candidate.Pid, out var pidMatches))
            {
                unresolved.Add(candidate);
                continue;
            }
            if (pidMatches.Length > 1)
                warnings.Add($"{candidate.RelativeSourcePath}: multiple compared save Pokémon have PID {candidate.Pid:X8}.");
            switch (options.SamePidMode)
            {
                case SamePidImportMode.ImportAdditionally:
                    decisions.Add(ImportPending(candidate, DatabaseDecisionRule.SamePid, "Same PID exists; configured to import additionally.", pidMatches));
                    break;
                case SamePidImportMode.DoNotImport:
                    decisions.Add(Skip(candidate, DatabaseDecisionRule.SamePid, "Same PID already exists and the configured rule skips it.", pidMatches));
                    break;
                case SamePidImportMode.ReplaceWhenMoreAdvanced:
                    var sameSpecies = pidMatches.Where(x => x.Species == candidate.Species).ToArray();
                    if (sameSpecies.Length == 0)
                    {
                        warnings.Add($"{candidate.RelativeSourcePath}: same PID found on a different species; importing additionally.");
                        decisions.Add(ImportPending(candidate, DatabaseDecisionRule.SamePid, "Same PID exists on a different species; imported additionally.", pidMatches));
                    }
                    else
                    {
                        var bestCompared = BestExisting(sameSpecies);
                        if (!IsBetter(candidate, bestCompared))
                        {
                            decisions.Add(Skip(candidate, DatabaseDecisionRule.SamePid, $"Same PID and species; database Pokémon is not more advanced than the {AreaName(bestCompared.Area)} Pokémon.", pidMatches));
                            break;
                        }

                        var replaceable = sameSpecies.Where(x => IsReplaceable(x, options)).ToArray();
                        if (replaceable.Length == 0)
                        {
                            warnings.Add($"{candidate.RelativeSourcePath}: the more advanced database Pokémon matches a non-replaceable {AreaName(bestCompared.Area)} Pokémon; importing additionally instead of replacing it.");
                            decisions.Add(ImportPending(candidate, DatabaseDecisionRule.SamePid, $"Same PID and species exists only in {AreaName(bestCompared.Area)}; that location is not enabled as a replacement target, so the database Pokémon is imported additionally.", pidMatches));
                            break;
                        }

                        var targetPokemon = BestExisting(replaceable);
                        var target = TargetKey(targetPokemon);
                        if (replacementTargets.Add(target))
                            decisions.Add(Replace(candidate, DatabaseDecisionRule.SamePid, $"Same PID and species; database Pokémon is more advanced than every compared match, so the best enabled {AreaName(targetPokemon.Area)} match is replaced.", pidMatches, targetPokemon));
                        else
                            decisions.Add(Skip(candidate, DatabaseDecisionRule.SamePid, "Replacement target is already used by an earlier database record.", pidMatches));
                    }
                    break;
            }
        }

        ResolveSpecies(unresolved, speciesIndex, options.SameSpeciesShinyMode, options.SpeciesShinyGrouping, decisions, replacementTargets);
        var pendingImports = decisions.Where(x => x.Kind == DatabaseDecisionKind.NewImport).ToArray();
        if (pendingImports.Length > slots.Length)
            errors.Add($"{pendingImports.Length} new-import slots are required, but only {slots.Length} enabled empty destinations are available. Replacements: {decisions.Count(x => x.Kind == DatabaseDecisionKind.Replacement)}.");
        var slotIndex = 0;
        for (var i = 0; i < decisions.Count; i++)
        {
            var decision = decisions[i];
            if (decision.Kind != DatabaseDecisionKind.NewImport)
                continue;
            if (slotIndex < slots.Length)
                decisions[i] = decision with { ImportDestination = slots[slotIndex++] };
            else
                decisions[i] = decision with { Kind = DatabaseDecisionKind.Skipped, Rule = DatabaseDecisionRule.Capacity, Reason = "Insufficient enabled destination capacity." };
        }

        decisions = decisions.OrderBy(x => x.Candidate.RelativeSourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Candidate.StableId, StringComparer.Ordinal).ToList();
        var incompatible = decisions.Count(x => x.Rule == DatabaseDecisionRule.Compatibility);
        var summary = new DatabaseImportSummary(
            filesScanned == 0 ? database.Count : filesScanned,
            database.Count,
            eligible.Count,
            pidComparison.Length,
            decisions.Count(x => x.Kind == DatabaseDecisionKind.NewImport),
            decisions.Count(x => x.Kind == DatabaseDecisionKind.Replacement),
            decisions.Count(x => x.Kind == DatabaseDecisionKind.Skipped),
            slots.Length,
            Math.Max(0, slots.Length - slotIndex),
            unreadableFiles,
            incompatible,
            stats);
        return new DatabaseImportPlan(options, decisions, summary, warnings.Distinct().Order(StringComparer.Ordinal), errors);
    }

    private static IReadOnlyList<DatabasePokemonCandidate> ReduceSamePidSpeciesBatch(
        IReadOnlyList<DatabasePokemonCandidate> candidates,
        SamePidImportMode mode,
        ICollection<DatabaseImportDecision> decisions)
    {
        if (mode != SamePidImportMode.ReplaceWhenMoreAdvanced)
            return candidates;

        var retained = new List<DatabasePokemonCandidate>(candidates.Count);
        foreach (var group in candidates.GroupBy(candidate => (candidate.Pid, candidate.Species))
                     .OrderBy(group => group.Key.Pid)
                     .ThenBy(group => group.Key.Species))
        {
            var ranked = group
                .OrderByDescending(candidate => candidate.Level)
                .ThenByDescending(candidate => candidate.Experience)
                .ThenBy(candidate => candidate.RelativeSourcePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal)
                .ToArray();
            var best = ranked[0];
            retained.Add(best);
            foreach (var duplicate in ranked.Skip(1))
            {
                decisions.Add(Skip(
                    duplicate,
                    DatabaseDecisionRule.SamePid,
                    $"Another database Pokémon with the same PID and species is more advanced or equivalent; {best.RelativeSourcePath} was retained for this import batch."));
            }
        }

        return retained
            .OrderBy(candidate => candidate.RelativeSourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal)
            .ToArray();
    }

    private static (List<DatabasePokemonCandidate> Eligible, List<DatabaseImportDecision> Filtered, DatabaseFilterStatistics Stats) Filter(
        IEnumerable<DatabasePokemonCandidate> source, PkmDatabaseFilterOptions filters)
    {
        var eligible = new List<DatabasePokemonCandidate>();
        var skipped = new List<DatabaseImportDecision>();
        var legality = 0; var origin = 0; var level = 0; var gender = 0; var shiny = 0;
        foreach (var item in source)
        {
            if (filters.Legality == LegalityFilterMode.OnlyLegal && item.IsLegal != true) { legality++; skipped.Add(Skip(item, DatabaseDecisionRule.Filter, "Excluded by legality filter.")); }
            else if (filters.OriginGame is { } game && item.OriginGameId != game) { origin++; skipped.Add(Skip(item, DatabaseDecisionRule.Filter, "Excluded by origin-game filter.")); }
            else if (filters.MinimumLevel is { } min && item.Level < min) { level++; skipped.Add(Skip(item, DatabaseDecisionRule.Filter, "Excluded by minimum-level filter.")); }
            else if (filters.Gender is { } wanted && item.Gender != wanted) { gender++; skipped.Add(Skip(item, DatabaseDecisionRule.Filter, "Excluded by gender filter.")); }
            else if (filters.IsShiny is { } wantedShiny && item.IsShiny != wantedShiny) { shiny++; skipped.Add(Skip(item, DatabaseDecisionRule.Filter, "Excluded by shiny-status filter.")); }
            else eligible.Add(item);
        }
        return (eligible, skipped, new(legality, origin, level, gender, shiny));
    }

    private static void ResolveSpecies(
        IReadOnlyList<DatabasePokemonCandidate> candidates,
        IReadOnlyDictionary<(int Species, bool? Shiny), ExistingSavePokemon[]> index,
        SameSpeciesShinyImportMode mode,
        SpeciesShinyGroupingMode grouping,
        ICollection<DatabaseImportDecision> decisions,
        ISet<(ExistingPokemonArea Area, int Facility, int Box, int Slot)> targets)
    {
        if (mode != SameSpeciesShinyImportMode.BestDatabaseRepresentativeReplaceWhenBetter)
        {
            foreach (var c in candidates)
            {
                var matches = index.GetValueOrDefault(SpeciesKey(c.Species, c.IsShiny, grouping), []);
                decisions.Add(mode == SameSpeciesShinyImportMode.DoNotImportWhenExisting && matches.Length != 0
                    ? Skip(c, DatabaseDecisionRule.SameSpeciesAndShiny, $"Matching {SpeciesMatchName(grouping)} already exists.", matches)
                    : ImportPending(c, matches.Length == 0 ? DatabaseDecisionRule.NoConflict : DatabaseDecisionRule.SameSpeciesAndShiny,
                        matches.Length == 0 ? $"No matching PID or {SpeciesMatchName(grouping)} conflict." : $"Matching {SpeciesMatchName(grouping)} is configured to import additionally.", matches));
            }
            return;
        }
        foreach (var group in candidates.GroupBy(x => SpeciesKey(x.Species, x.IsShiny, grouping))
                     .OrderBy(x => x.Key.Species).ThenBy(x => x.Key.Shiny))
        {
            var ranked = group.OrderByDescending(x => x.Level).ThenByDescending(x => x.Experience)
                .ThenBy(x => x.RelativeSourcePath, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray();
            var best = ranked[0];
            foreach (var other in ranked.Skip(1))
                decisions.Add(Skip(other, DatabaseDecisionRule.SameSpeciesAndShiny, "A stronger or equivalent database representative was selected."));
            var matches = index.GetValueOrDefault(group.Key, []);
            if (matches.Length == 0) { decisions.Add(ImportPending(best, DatabaseDecisionRule.SameSpeciesAndShiny, "Best database representative; no matching save Pokémon.")); continue; }
            var saveBest = BestExisting(matches);
            var target = TargetKey(saveBest);
            if (IsBetter(best, saveBest) && targets.Add(target))
                decisions.Add(Replace(best, DatabaseDecisionRule.SameSpeciesAndShiny, "Best database representative is more advanced than the best matching save Pokémon.", matches, saveBest));
            else
                decisions.Add(Skip(best, DatabaseDecisionRule.SameSpeciesAndShiny, IsBetter(best, saveBest) ? "Replacement target is already used by an earlier PID decision." : "Best database representative is not more advanced than the best matching save Pokémon.", matches));
        }
    }

    private static ExistingSavePokemon BestExisting(IEnumerable<ExistingSavePokemon> items) =>
        items.OrderByDescending(x => x.Level).ThenByDescending(x => x.Experience).ThenBy(x => x.Area)
            .ThenBy(x => x.FacilityIndex).ThenBy(x => x.BoxIndex).ThenBy(x => x.SlotIndex).ThenBy(x => x.StableId, StringComparer.Ordinal).First();
    private static bool IsBetter(DatabasePokemonCandidate incoming, ExistingSavePokemon current) =>
        incoming.Level > current.Level || incoming.Level == current.Level && incoming.Experience > current.Experience;
    private static bool IsReplaceable(ExistingSavePokemon pokemon, PkmDatabaseImportOptions options) =>
        pokemon.Area == ExistingPokemonArea.Box ||
        pokemon.Area == ExistingPokemonArea.Team && options.AllowTeamReplacements;
    private static (ExistingPokemonArea Area, int Facility, int Box, int Slot) TargetKey(ExistingSavePokemon pokemon) =>
        (pokemon.Area, pokemon.FacilityIndex, pokemon.BoxIndex, pokemon.SlotIndex);
    private static (int Species, bool? Shiny) SpeciesKey(int species, bool shiny, SpeciesShinyGroupingMode grouping) =>
        (species, grouping == SpeciesShinyGroupingMode.Separate ? shiny : null);
    private static string SpeciesMatchName(SpeciesShinyGroupingMode grouping) =>
        grouping == SpeciesShinyGroupingMode.Separate ? "species and shiny-status group" : "species";
    private static DatabaseImportDecision ImportPending(DatabasePokemonCandidate c, DatabaseDecisionRule rule, string reason, IReadOnlyList<ExistingSavePokemon>? matches = null) =>
        new(c, DatabaseDecisionKind.NewImport, rule, reason, matches ?? [], null, null);
    private static DatabaseImportDecision Replace(DatabasePokemonCandidate c, DatabaseDecisionRule rule, string reason, IReadOnlyList<ExistingSavePokemon> matches, ExistingSavePokemon target) =>
        new(c, DatabaseDecisionKind.Replacement, rule, reason, matches, null, target);
    private static DatabaseImportDecision Skip(DatabasePokemonCandidate c, DatabaseDecisionRule rule, string reason, IReadOnlyList<ExistingSavePokemon>? matches = null) =>
        new(c, DatabaseDecisionKind.Skipped, rule, reason, matches ?? [], null, null);
    private static string AreaName(ExistingPokemonArea area) => area switch
    {
        ExistingPokemonArea.Team => "Team",
        ExistingPokemonArea.Pension => "Pension",
        _ => "selected-box",
    };
    private static List<string> Validate(PkmDatabaseImportOptions options)
    {
        var errors = new List<string>();
        if (options.SelectedBoxIndices.Count == 0) errors.Add("Select at least one box.");
        if (options.Filters.MinimumLevel is < 1 or > 100) errors.Add("Minimum level must be from 1 through 100.");
        if (options.Filters.OriginGame is < 0) errors.Add("Origin game ID is invalid.");
        if (options.AllowTeamReplacements && !options.IncludeTeamInPidComparison)
            errors.Add("Enable Team PID comparison before allowing Team replacements.");
        return errors;
    }
}
