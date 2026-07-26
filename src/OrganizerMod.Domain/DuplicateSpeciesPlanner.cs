namespace OrganizerMod.Domain;

public sealed class DuplicateSpeciesPlanner
{
    public SpeciesDuplicateRemovalPlan CreatePlan(
        IReadOnlyList<DuplicateCandidate> pokemon,
        DuplicateSpeciesOptions options)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Criteria);
        ArgumentNullException.ThrowIfNull(options.SelectedBoxIndices);

        var errors = Validate(options);
        var scanned = pokemon
            .Where(item => options.SelectedBoxIndices.Contains(item.Reference.SourceBoxIndex))
            .OrderBy(Location)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToArray();
        var eggs = scanned.Count(item => item.IsEgg);
        var invalid = scanned.Count(item => !item.IsEgg && (!item.IsValid || item.Species <= 0));
        var ignoredShiny = options.ShinyMode == ShinyDuplicateMode.IgnoreShiny
            ? scanned.Count(item => !item.IsEgg && item.IsValid && item.Species > 0 && item.IsShiny)
            : 0;
        var analyzed = scanned
            .Where(item => !item.IsEgg && item.IsValid && item.Species > 0)
            .Where(item => options.ShinyMode != ShinyDuplicateMode.IgnoreShiny || !item.IsShiny)
            .ToArray();

        var decisions = analyzed
            .GroupBy(item => GetKey(item, options.ShinyMode))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.Species)
            .ThenBy(group => group.Key.IsShiny)
            .Select(group => CreateDecision(group.Key, group.ToArray(), options.Criteria))
            .ToArray();
        var summary = new DuplicateRemovalSummary(
            options.SelectedBoxIndices.Count,
            scanned.Length,
            analyzed.Length,
            analyzed.Select(item => item.Species).Distinct().Count(),
            decisions.Length,
            decisions.Length,
            decisions.Sum(item => item.Removed.Count),
            eggs,
            invalid,
            ignoredShiny);
        return new SpeciesDuplicateRemovalPlan(options, decisions, summary, errors);
    }

    private static List<string> Validate(DuplicateSpeciesOptions options)
    {
        var errors = new List<string>();
        var duplicateTypes = options.Criteria
            .GroupBy(item => item.Type)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var type in duplicateTypes)
            errors.Add($"Criterion {type} appears more than once.");

        foreach (var criterion in options.Criteria.Where(item => item.Enabled))
        {
            switch (criterion.Type)
            {
                case DuplicateSelectionCriterionType.HighestLevel:
                    break;
                case DuplicateSelectionCriterionType.PreferredOriginGame:
                    if (criterion.PreferredOriginGame is null)
                        errors.Add("Preferred origin game requires a selected game.");
                    else if (options.SupportedOriginGameIds is { } supported &&
                             !supported.Contains(criterion.PreferredOriginGame.Value))
                        errors.Add($"Origin game ID {criterion.PreferredOriginGame} is not supported.");
                    break;
                case DuplicateSelectionCriterionType.PreferredGender:
                    if (criterion.PreferredGender is null)
                        errors.Add("Preferred gender requires a selected gender.");
                    break;
                default:
                    errors.Add($"Unsupported duplicate-selection criterion: {criterion.Type}.");
                    break;
            }
        }
        return errors;
    }

    private static DuplicateGroupKey GetKey(
        DuplicateCandidate candidate,
        ShinyDuplicateMode mode) =>
        new(
            candidate.Species,
            mode == ShinyDuplicateMode.SeparateShinyGroup
                ? candidate.IsShiny
                : null);

    private static DuplicateRemovalDecision CreateDecision(
        DuplicateGroupKey key,
        IReadOnlyList<DuplicateCandidate> candidates,
        IReadOnlyList<DuplicateSelectionCriterion> criteria)
    {
        var remaining = candidates
            .OrderBy(Location)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToList();
        var reasons = new List<string>();
        foreach (var criterion in criteria.Where(item => item.Enabled))
        {
            var preferred = ApplyCriterion(remaining, criterion);
            if (preferred.Count == remaining.Count)
                continue;
            remaining = preferred;
            reasons.Add(Describe(criterion));
            if (remaining.Count == 1)
                break;
        }

        var kept = remaining
            .OrderBy(Location)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .First();
        if (remaining.Count > 1 || reasons.Count == 0)
            reasons.Add("Tie resolved by earliest original box and slot.");
        var removed = candidates
            .Where(item => item.Reference != kept.Reference)
            .OrderBy(Location)
            .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
            .ToArray();
        return new DuplicateRemovalDecision(
            key,
            kept,
            Array.AsReadOnly(removed),
            Array.AsReadOnly(reasons.ToArray()));
    }

    private static List<DuplicateCandidate> ApplyCriterion(
        IReadOnlyList<DuplicateCandidate> candidates,
        DuplicateSelectionCriterion criterion)
    {
        switch (criterion.Type)
        {
            case DuplicateSelectionCriterionType.HighestLevel:
                var highest = candidates.Max(item => item.Level);
                return candidates.Where(item => item.Level == highest).ToList();
            case DuplicateSelectionCriterionType.PreferredOriginGame:
                var origin = candidates
                    .Where(item => item.OriginGameId == criterion.PreferredOriginGame)
                    .ToList();
                return origin.Count == 0 ? candidates.ToList() : origin;
            case DuplicateSelectionCriterionType.PreferredGender:
                var gender = candidates
                    .Where(item => item.Gender == criterion.PreferredGender)
                    .ToList();
                return gender.Count == 0 ? candidates.ToList() : gender;
            default:
                throw new ArgumentOutOfRangeException(nameof(criterion));
        }
    }

    private static string Describe(DuplicateSelectionCriterion criterion) =>
        criterion.Type switch
        {
            DuplicateSelectionCriterionType.HighestLevel => "Preferred the highest current level.",
            DuplicateSelectionCriterionType.PreferredOriginGame =>
                $"Matched preferred origin game ID {criterion.PreferredOriginGame}.",
            DuplicateSelectionCriterionType.PreferredGender =>
                $"Matched preferred gender {criterion.PreferredGender}.",
            _ => throw new ArgumentOutOfRangeException(nameof(criterion)),
        };

    private static (int Box, int Slot) Location(DuplicateCandidate item) =>
        (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex);
}
