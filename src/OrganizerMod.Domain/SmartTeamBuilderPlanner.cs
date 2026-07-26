namespace OrganizerMod.Domain;

public sealed class SmartTeamBuilderPlanner
{
    public TeamExchangePlan CreatePlan(
        IReadOnlyList<TeamBuilderCandidate> candidates,
        IReadOnlyList<SlotPosition> emptySelectedBoxSlots,
        TeamBuilderOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(emptySelectedBoxSlots);
        ArgumentNullException.ThrowIfNull(options);
        var errors = Validate(candidates, emptySelectedBoxSlots, options);
        var warnings = new List<string>();
        var exclusion = new Dictionary<string, int>(StringComparer.Ordinal);
        if (errors.Count != 0)
            return Empty(options, candidates, errors, exclusion);

        var eligible = candidates.Where(candidate => candidate.IsValid).ToList();
        exclusion["Invalid Pokémon"] = candidates.Count - eligible.Count;
        if (!options.AllowEggs)
        {
            var before = eligible.Count;
            eligible.RemoveAll(candidate => candidate.IsEgg);
            exclusion["Eggs"] = before - eligible.Count;
        }

        foreach (var rule in options.EligibilityRules.Where(rule => rule.Enabled))
        {
            var before = eligible.Count;
            eligible = eligible.Where(candidate => Matches(candidate, rule)).ToList();
            exclusion[Describe(rule)] = before - eligible.Count;
        }

        var ranked = eligible.OrderBy(candidate => candidate, new CandidateComparer(options.PreferenceCriteria)).ToArray();
        var selected = Select(ranked, options.RequestedTeamSize, options.PreferDifferentSpecies);
        if (selected.Count < options.RequestedTeamSize)
        {
            var message = $"Only {selected.Count} eligible Pokémon are available for the requested Team size of {options.RequestedTeamSize}.";
            if (!options.AllowSmallerTeam)
                errors.Add(message + " Lower the Team size or enable the smaller-Team option.");
            else
                warnings.Add(message + " The preview uses the smaller Team.");
        }

        if (errors.Count != 0)
            return Empty(options, candidates, errors, exclusion, eligible.Count, warnings);

        var orderedTeam = OrderTeam(selected, options.PartyOrder, options.PreferenceCriteria);
        var selectedIds = orderedTeam.Select(candidate => candidate.StableId).ToHashSet(StringComparer.Ordinal);
        var displaced = candidates
            .Where(candidate => candidate.OriginalLocation.IsParty && !selectedIds.Contains(candidate.StableId))
            .OrderBy(candidate => candidate.OriginalLocation.Slot)
            .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal)
            .ToArray();
        var vacated = candidates
            .Where(candidate => candidate.OriginalLocation.Area == PokemonStorageArea.Box && selectedIds.Contains(candidate.StableId))
            .Select(candidate => new SlotPosition(candidate.OriginalLocation.Box, candidate.OriginalLocation.Slot))
            .OrderBy(slot => slot.Box).ThenBy(slot => slot.Slot)
            .ToArray();
        var destinations = vacated.Concat(emptySelectedBoxSlots.OrderBy(slot => slot.Box).ThenBy(slot => slot.Slot))
            .Distinct().ToArray();
        if (destinations.Length < displaced.Length)
        {
            errors.Add($"{displaced.Length} Team Pokémon must be moved into storage, but only {destinations.Length} usable storage slot(s) are available in the selected boxes.");
            return Empty(options, candidates, errors, exclusion, eligible.Count, warnings);
        }

        var boxAssignments = candidates
            .Where(candidate => candidate.OriginalLocation.Area == PokemonStorageArea.Box && !selectedIds.Contains(candidate.StableId))
            .Select(candidate => new TeamBoxAssignment(candidate.StableId, candidate.OriginalLocation.Box, candidate.OriginalLocation.Slot))
            .ToList();
        for (var index = 0; index < displaced.Length; index++)
            boxAssignments.Add(new TeamBoxAssignment(displaced[index].StableId, destinations[index].Box, destinations[index].Slot));

        var decisions = orderedTeam.Select((candidate, slot) => new TeamSelectionDecision(
            candidate.StableId, slot, BuildReasons(candidate, options.PreferenceCriteria))).ToArray();
        var finalLocations = decisions.ToDictionary(x => x.StableId, x => PokemonStorageLocation.Party(x.FinalTeamSlot), StringComparer.Ordinal);
        foreach (var assignment in boxAssignments)
            finalLocations[assignment.StableId] = PokemonStorageLocation.BoxSlot(assignment.BoxIndex, assignment.SlotIndex);
        var changes = candidates
            .Where(candidate => finalLocations.TryGetValue(candidate.StableId, out var destination) && destination != candidate.OriginalLocation)
            .Select(candidate => new TeamLocationChange(candidate.StableId, candidate.OriginalLocation, finalLocations[candidate.StableId]))
            .OrderBy(change => change.Source.Area).ThenBy(change => change.Source.Box).ThenBy(change => change.Source.Slot)
            .ToArray();

        var retained = orderedTeam.Count(candidate => candidate.OriginalLocation.IsParty);
        var movedFromBoxes = orderedTeam.Count - retained;
        var unchangedBoxes = candidates.Count(candidate => candidate.OriginalLocation.Area == PokemonStorageArea.Box &&
            !changes.Any(change => change.StableId == candidate.StableId));
        var summary = new TeamBuilderSummary(options.SelectedBoxIndices.Count, candidates.Count, eligible.Count,
            options.RequestedTeamSize, orderedTeam.Count, retained, movedFromBoxes, displaced.Length,
            unchangedBoxes, exclusion.GetValueOrDefault("Invalid Pokémon"), exclusion.GetValueOrDefault("Eggs"));
        return new TeamExchangePlan(options, decisions, boxAssignments, changes, summary, warnings, errors, exclusion);
    }

    private static List<string> Validate(IReadOnlyList<TeamBuilderCandidate> candidates, IReadOnlyList<SlotPosition> emptySlots, TeamBuilderOptions options)
    {
        var errors = new List<string>();
        if (options.MaximumTeamSize is < 1 or > 6 || options.RequestedTeamSize < 1 || options.RequestedTeamSize > options.MaximumTeamSize)
            errors.Add($"Team size must be between 1 and {options.MaximumTeamSize}.");
        if (options.SelectedBoxIndices.Count == 0) errors.Add("Select at least one storage box.");
        if (candidates.Select(x => x.StableId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            errors.Add("Candidate identities must be unique.");
        if (candidates.Any(x => x.OriginalLocation.Area == PokemonStorageArea.Box && !options.SelectedBoxIndices.Contains(x.OriginalLocation.Box)))
            errors.Add("A candidate came from an unselected box.");
        if (candidates.Any(x => x.OriginalLocation.IsParty && !x.IsValid))
            errors.Add("The current Team contains invalid Pokémon data that cannot be moved safely.");
        if (emptySlots.Any(x => !options.SelectedBoxIndices.Contains(x.Box))) errors.Add("An empty destination belongs to an unselected box.");
        ValidateRules(options.EligibilityRules, errors);
        ValidatePreferences(options.PreferenceCriteria, errors);
        return errors;
    }

    private static void ValidateRules(IReadOnlyList<TeamEligibilityRule> rules, List<string> errors)
    {
        if (rules.Where(x => x.Enabled).GroupBy(x => x.Type).Any(x => x.Count() > 1)) errors.Add("Eligibility rules cannot be duplicated.");
        foreach (var rule in rules.Where(x => x.Enabled))
        {
            if (rule.Type == TeamEligibilityRuleType.RequiredTypes && !ValidTypes(rule.Types)) errors.Add("Required types must contain one or two distinct valid types.");
            if (rule.Type == TeamEligibilityRuleType.RequiredOriginGame && rule.OriginGame is null or <= 0) errors.Add("Required origin game is missing.");
            if (rule.Type == TeamEligibilityRuleType.RequiredSpeciesGeneration && rule.SpeciesGeneration is null or < 1 or > 9) errors.Add("Required species generation must be between 1 and 9.");
        }
    }

    private static void ValidatePreferences(IReadOnlyList<TeamPreferenceCriterion> criteria, List<string> errors)
    {
        if (criteria.Where(x => x.Enabled).GroupBy(x => x.Type).Any(x => x.Count() > 1)) errors.Add("Preference criteria cannot be duplicated.");
        foreach (var criterion in criteria.Where(x => x.Enabled))
        {
            if (criterion.Type == TeamPreferenceCriterionType.PreferredTypes && !ValidTypes(criterion.Types)) errors.Add("Preferred types must contain one or two distinct valid types.");
            if (criterion.Type == TeamPreferenceCriterionType.PreferredOriginGame && criterion.OriginGame is null or <= 0) errors.Add("Preferred origin game is missing.");
            if (criterion.Type == TeamPreferenceCriterionType.PreferredSpeciesGeneration && criterion.SpeciesGeneration is null or < 1 or > 9) errors.Add("Preferred species generation must be between 1 and 9.");
        }
    }

    private static bool ValidTypes(IReadOnlyList<PokemonElementType>? types) =>
        types is { Count: >= 1 and <= 2 } && types.Distinct().Count() == types.Count &&
        types.All(type => type is >= PokemonElementType.Normal and <= PokemonElementType.Fairy);

    private static bool Matches(TeamBuilderCandidate candidate, TeamEligibilityRule rule) => rule.Type switch
    {
        TeamEligibilityRuleType.RequiredTypes => MatchesTypes(candidate, rule.Types!, rule.TypeMatching),
        TeamEligibilityRuleType.RequiredOriginGame => candidate.OriginGame == rule.OriginGame,
        TeamEligibilityRuleType.RequiredSpeciesGeneration => candidate.SpeciesGeneration == rule.SpeciesGeneration,
        TeamEligibilityRuleType.LegendaryOrMythicalOnly => candidate.IsLegendaryOrMythical,
        TeamEligibilityRuleType.ShinyOnly => candidate.IsShiny,
        _ => false,
    };

    public static bool MatchesTypes(TeamBuilderCandidate candidate, IReadOnlyList<PokemonElementType> selected, TeamTypeMatchingMode mode)
    {
        var actual = candidate.Types.ToHashSet();
        var wanted = selected.ToHashSet();
        return mode switch
        {
            TeamTypeMatchingMode.HasAnySelectedType => wanted.Overlaps(actual),
            TeamTypeMatchingMode.HasAllSelectedTypes => wanted.IsSubsetOf(actual),
            TeamTypeMatchingMode.ExactTypeCombination => wanted.SetEquals(actual),
            _ => false,
        };
    }

    private static List<TeamBuilderCandidate> Select(IReadOnlyList<TeamBuilderCandidate> ranked, int count, bool diverse)
    {
        if (!diverse) return ranked.Take(count).ToList();
        var result = new List<TeamBuilderCandidate>(count);
        var species = new HashSet<int>();
        foreach (var candidate in ranked)
            if (species.Add(candidate.Species) && result.Count < count) result.Add(candidate);
        if (result.Count < count)
            foreach (var candidate in ranked)
                if (!result.Contains(candidate) && result.Count < count) result.Add(candidate);
        return result;
    }

    private static List<TeamBuilderCandidate> OrderTeam(
        IReadOnlyList<TeamBuilderCandidate> selected,
        TeamPartyOrder order,
        IReadOnlyList<TeamPreferenceCriterion> criteria)
    {
        var comparer = new CandidateComparer(criteria);
        return order switch
        {
            TeamPartyOrder.PreferenceOrder => selected.OrderBy(x => x, comparer).ToList(),
            TeamPartyOrder.PreserveCurrentTeamOrder => selected.Where(x => x.OriginalLocation.IsParty).OrderBy(x => x.OriginalLocation.Slot)
                .Concat(selected.Where(x => !x.OriginalLocation.IsParty).OrderBy(x => x, comparer)).ToList(),
            TeamPartyOrder.LevelDescending => selected.OrderByDescending(x => x.Level).ThenByDescending(x => x.Experience).ThenBy(x => x, comparer).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(order)),
        };
    }

    private static string Describe(TeamEligibilityRule rule) => rule.Type switch
    {
        TeamEligibilityRuleType.RequiredTypes => $"Required types ({rule.TypeMatching})",
        TeamEligibilityRuleType.RequiredOriginGame => "Required origin game",
        TeamEligibilityRuleType.RequiredSpeciesGeneration => "Required species generation",
        TeamEligibilityRuleType.LegendaryOrMythicalOnly => "Legendary or Mythical only",
        TeamEligibilityRuleType.ShinyOnly => "Shiny only",
        _ => rule.Type.ToString(),
    };

    private static IReadOnlyList<string> BuildReasons(TeamBuilderCandidate candidate, IReadOnlyList<TeamPreferenceCriterion> criteria)
    {
        var result = new List<string> { "Matched all enabled eligibility rules." };
        foreach (var criterion in criteria.Where(x => x.Enabled))
            result.Add(criterion.Type switch
            {
                TeamPreferenceCriterionType.HighestLevelAndExperience => $"Level {candidate.Level}, EXP {candidate.Experience:N0}.",
                TeamPreferenceCriterionType.PreferredTypes => $"Matches {candidate.Types.Count(type => criterion.Types!.Contains(type))} preferred type(s).",
                TeamPreferenceCriterionType.PreferredOriginGame => candidate.OriginGame == criterion.OriginGame ? "Matches the preferred origin game." : "Does not match the preferred origin game.",
                TeamPreferenceCriterionType.PreferredSpeciesGeneration => candidate.SpeciesGeneration == criterion.SpeciesGeneration ? "Matches the preferred species generation." : "Does not match the preferred species generation.",
                TeamPreferenceCriterionType.PreferLegendaryOrMythical => candidate.IsLegendaryOrMythical ? "Legendary or Mythical preference matched." : "Ordinary species.",
                TeamPreferenceCriterionType.PreferShiny => candidate.IsShiny ? "Shiny preference matched." : "Non-shiny.",
                _ => criterion.Type.ToString(),
            });
        result.Add("Final ties favor the current Team, then original location.");
        return result;
    }

    private static TeamExchangePlan Empty(TeamBuilderOptions options, IReadOnlyList<TeamBuilderCandidate> candidates,
        IEnumerable<string> errors, IReadOnlyDictionary<string, int> exclusion, int eligible = 0, IEnumerable<string>? warnings = null) =>
        new(options, [], [], [], new TeamBuilderSummary(options.SelectedBoxIndices.Count, candidates.Count, eligible,
            options.RequestedTeamSize, 0, 0, 0, 0, candidates.Count(x => x.OriginalLocation.Area == PokemonStorageArea.Box), 0, 0),
            warnings ?? [], errors, exclusion);

    private sealed class CandidateComparer(IReadOnlyList<TeamPreferenceCriterion> criteria) : IComparer<TeamBuilderCandidate>
    {
        public int Compare(TeamBuilderCandidate? x, TeamBuilderCandidate? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;
            if (y is null) return -1;
            foreach (var criterion in criteria.Where(x => x.Enabled))
            {
                var result = criterion.Type switch
                {
                    TeamPreferenceCriterionType.HighestLevelAndExperience => CompareDescending(x.Level, y.Level, x.Experience, y.Experience),
                    TeamPreferenceCriterionType.PreferredTypes => Count(y, criterion.Types!).CompareTo(Count(x, criterion.Types!)),
                    TeamPreferenceCriterionType.PreferredOriginGame => Match(y.OriginGame, criterion.OriginGame).CompareTo(Match(x.OriginGame, criterion.OriginGame)),
                    TeamPreferenceCriterionType.PreferredSpeciesGeneration => Match(y.SpeciesGeneration, criterion.SpeciesGeneration).CompareTo(Match(x.SpeciesGeneration, criterion.SpeciesGeneration)),
                    TeamPreferenceCriterionType.PreferLegendaryOrMythical => y.IsLegendaryOrMythical.CompareTo(x.IsLegendaryOrMythical),
                    TeamPreferenceCriterionType.PreferShiny => y.IsShiny.CompareTo(x.IsShiny),
                    _ => 0,
                };
                if (result != 0) return result;
            }
            var area = (x.OriginalLocation.IsParty ? 0 : 1).CompareTo(y.OriginalLocation.IsParty ? 0 : 1);
            if (area != 0) return area;
            var box = x.OriginalLocation.Box.CompareTo(y.OriginalLocation.Box);
            if (box != 0) return box;
            var slot = x.OriginalLocation.Slot.CompareTo(y.OriginalLocation.Slot);
            return slot != 0 ? slot : StringComparer.Ordinal.Compare(x.StableId, y.StableId);
        }
        private static int CompareDescending(int xl, int yl, ulong xe, ulong ye) { var level = yl.CompareTo(xl); return level != 0 ? level : ye.CompareTo(xe); }
        private static int Count(TeamBuilderCandidate x, IReadOnlyList<PokemonElementType> types) => x.Types.Count(types.Contains);
        private static bool Match(int actual, int? expected) => expected is not null && actual == expected;
    }
}
