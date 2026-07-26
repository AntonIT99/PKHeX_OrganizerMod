namespace OrganizerMod.Domain;

public sealed class LivingDexOrganizationPlanner
{
    public const int BoxCapacity = 30;

    public LivingDexOrganizationPlan CreatePlan(
        IReadOnlyList<LivingDexCandidate> pokemon,
        IReadOnlyList<LivingDexEntryDefinition> definitions,
        IReadOnlyList<BoxState> boxes,
        LivingDexOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);

        var errors = ValidateInput(pokemon, definitions, boxes);
        var orderedDefinitions = definitions.OrderBy(item => item.Key).ToArray();
        if (errors.Count != 0)
            return Invalid(options, boxes.Count, pokemon.Count, orderedDefinitions, errors);

        var preserved = pokemon
            .Where(item => ShouldPreserve(item, options))
            .OrderBy(item => item.Reference.SourceBoxIndex)
            .ThenBy(item => item.Reference.SourceSlotIndex)
            .ToArray();
        var included = pokemon
            .Where(item => !ShouldPreserve(item, options))
            .ToArray();
        var preservedPositions = preserved
            .Select(item => (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex))
            .ToHashSet();
        var orderedBoxes = boxes.OrderBy(box => box.BoxIndex).ToArray();
        var availablePositions = orderedBoxes
            .SelectMany(box => Enumerable.Range(0, box.Capacity)
                .Select(slot => new SlotPosition(box.BoxIndex, slot)))
            .Where(position => !preservedPositions.Contains((position.Box, position.Slot)))
            .ToArray();

        if (included.Length > availablePositions.Length)
        {
            return Invalid(
                options,
                boxes.Count,
                pokemon.Count,
                orderedDefinitions,
                [$"{included.Length} Pokémon must be preserved in the generated layout, but only {availablePositions.Length} selected target slots are available after fixed preserved slots are excluded."],
                preserved.Select(item => item.Reference));
        }

        var definitionByKey = orderedDefinitions.ToDictionary(item => item.Key);
        var candidatesByKey = new Dictionary<LivingDexEntryKey, List<LivingDexCandidate>>();
        foreach (var candidate in included)
        {
            if (!CanSatisfyEntry(candidate, options))
                continue;
            var key = GetCandidateKey(candidate, options);
            if (!definitionByKey.ContainsKey(key))
                continue;
            if (!candidatesByKey.TryGetValue(key, out var candidates))
                candidatesByKey.Add(key, candidates = []);
            candidates.Add(candidate);
        }

        var representativeComparer = new RepresentativeComparer(options.RepresentativePreference);
        var representatives = new List<(LivingDexEntryDefinition Definition, LivingDexCandidate Candidate)>();
        var selectedReferences = new HashSet<PokemonReference>();
        var duplicateCount = 0;
        foreach (var definition in orderedDefinitions)
        {
            if (!candidatesByKey.TryGetValue(definition.Key, out var candidates))
                continue;
            candidates.Sort(representativeComparer);
            representatives.Add((definition, candidates[0]));
            selectedReferences.Add(candidates[0].Reference);
            duplicateCount += candidates.Count - 1;
        }

        var filledKeys = representatives.Select(item => item.Definition.Key).ToHashSet();
        var missing = orderedDefinitions
            .Where(definition => !filledKeys.Contains(definition.Key))
            .Select(definition => new MissingLivingDexEntry(definition))
            .ToArray();

        var overflow = included
            .Where(candidate => !selectedReferences.Contains(candidate.Reference))
            .ToArray();
        overflow = OrderOverflow(overflow, options, representativeComparer);

        var assignments = new List<LivingDexSlotAssignment>(included.Length);
        for (var index = 0; index < representatives.Count; index++)
        {
            var position = availablePositions[index];
            var representative = representatives[index];
            assignments.Add(new LivingDexSlotAssignment(
                representative.Candidate.Reference,
                position.Box,
                position.Slot,
                representative.Definition.Key,
                false));
        }

        var overflowPositions = GetOverflowPositions(
            availablePositions,
            representatives.Count,
            orderedBoxes,
            options.OverflowStart);
        if (overflow.Length > overflowPositions.Count)
        {
            return Invalid(
                options,
                boxes.Count,
                pokemon.Count,
                orderedDefinitions,
                [$"{included.Length} Pokémon fit in the selected boxes, but the chosen overflow boundary provides only {overflowPositions.Count} overflow slots for {overflow.Length} overflow Pokémon. Select more boxes or start overflow immediately after Living Dex entries."],
                preserved.Select(item => item.Reference),
                missing);
        }

        for (var index = 0; index < overflow.Length; index++)
        {
            var position = overflowPositions[index];
            assignments.Add(new LivingDexSlotAssignment(
                overflow[index].Reference,
                position.Box,
                position.Slot,
                null,
                true));
        }

        var boxesByIndex = assignments
            .GroupBy(item => item.TargetBoxIndex)
            .OrderBy(group => group.Key)
            .Select(group => new LivingDexBoxAssignment(
                group.Key,
                group.Where(item => !item.IsOverflow)
                    .OrderBy(item => item.TargetSlotIndex)
                    .Select(item => item.Pokemon),
                group.Where(item => item.IsOverflow)
                    .OrderBy(item => item.TargetSlotIndex)
                    .Select(item => item.Pokemon)))
            .ToArray();
        var boxStateByIndex = orderedBoxes.ToDictionary(box => box.BoxIndex);
        var renames = LivingDexBoxNameGenerator.CreateRenames(
            boxesByIndex,
            boxStateByIndex,
            options);
        var warnings = CreateWarnings(options, preserved.Length);
        var mainBoxes = boxesByIndex.Count(box => box.MainPokemon.Count != 0);
        var overflowBoxes = boxesByIndex.Count(box => box.OverflowPokemon.Count != 0);
        var requiredBoxes = assignments.Select(item => item.TargetBoxIndex).Distinct().Count();
        var completion = orderedDefinitions.Length == 0
            ? 100d
            : (representatives.Count * 100d) / orderedDefinitions.Length;
        var summary = new LivingDexSummary(
            orderedDefinitions.Length,
            representatives.Count,
            missing.Length,
            completion,
            included.Length,
            representatives.Count,
            duplicateCount,
            overflow.Length,
            preserved.Length,
            mainBoxes,
            overflowBoxes,
            requiredBoxes,
            boxes.Count,
            availablePositions.Length,
            availablePositions.Length - included.Length);

        return new LivingDexOrganizationPlan(
            options,
            assignments,
            boxesByIndex,
            preserved.Select(item => item.Reference),
            renames,
            missing,
            warnings,
            [],
            summary);
    }

    private static IReadOnlyList<string> ValidateInput(
        IReadOnlyList<LivingDexCandidate> pokemon,
        IReadOnlyList<LivingDexEntryDefinition> definitions,
        IReadOnlyList<BoxState> boxes)
    {
        var errors = new List<string>();
        if (boxes.Any(box => box.Capacity != BoxCapacity))
            errors.Add("Living Dex Sorting currently requires storage boxes with exactly 30 slots.");
        if (boxes.Select(box => box.BoxIndex).Distinct().Count() != boxes.Count)
            errors.Add("The selected box list contains duplicate box indices.");
        if (definitions.Select(item => item.Key).Distinct().Count() != definitions.Count)
            errors.Add("The Living Dex definition contains duplicate entry keys.");
        if (pokemon.Select(item => item.Reference.StableId).Distinct(StringComparer.Ordinal).Count() != pokemon.Count)
            errors.Add("The Pokémon input contains duplicate stable identities.");
        if (pokemon.Select(item => (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex)).Distinct().Count() != pokemon.Count)
            errors.Add("The Pokémon input contains duplicate source slots.");
        if (pokemon.Any(item => boxes.All(box => box.BoxIndex != item.Reference.SourceBoxIndex)))
            errors.Add("Every Pokémon must originate in one of the selected boxes.");
        return errors;
    }

    private static bool ShouldPreserve(
        LivingDexCandidate candidate,
        LivingDexOrganizerOptions options) =>
        (candidate.IsEgg && options.EggHandling == LivingDexEggHandling.ExcludeAndPreserve) ||
        (!candidate.HasValidData && options.InvalidHandling == LivingDexInvalidHandling.ExcludeAndPreserve);

    private static bool CanSatisfyEntry(
        LivingDexCandidate candidate,
        LivingDexOrganizerOptions options) =>
        !candidate.IsEgg &&
        candidate.HasValidData &&
        (options.Mode != LivingDexMode.Shiny || candidate.IsShiny);

    private static LivingDexEntryKey GetCandidateKey(
        LivingDexCandidate candidate,
        LivingDexOrganizerOptions options)
    {
        var form = options.Mode == LivingDexMode.Form ||
                   (options.Mode == LivingDexMode.Shiny &&
                    options.ShinyScope == LivingDexShinyScope.Form)
            ? candidate.Form
            : 0;
        return new LivingDexEntryKey(
            candidate.Species,
            form,
            options.Mode == LivingDexMode.Shiny);
    }

    private static LivingDexCandidate[] OrderOverflow(
        IEnumerable<LivingDexCandidate> overflow,
        LivingDexOrganizerOptions options,
        IComparer<LivingDexCandidate> qualityComparer) =>
        options.OverflowOrder switch
        {
            LivingDexOverflowOrder.NationalDex => overflow
                .OrderBy(item => item.Species)
                .ThenBy(item => item.Form)
                .ThenByDescending(item => item.IsShiny)
                .ThenBy(item => item.Reference.SourceBoxIndex)
                .ThenBy(item => item.Reference.SourceSlotIndex)
                .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
                .ToArray(),
            LivingDexOverflowOrder.OriginalPosition => overflow
                .OrderBy(item => item.Reference.SourceBoxIndex)
                .ThenBy(item => item.Reference.SourceSlotIndex)
                .ThenBy(item => item.Reference.StableId, StringComparer.Ordinal)
                .ToArray(),
            LivingDexOverflowOrder.SpeciesThenQuality => overflow
                .OrderBy(item => item.Species)
                .ThenBy(item => item.Form)
                .ThenBy(item => item, qualityComparer)
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(),
        };

    private static IReadOnlyList<SlotPosition> GetOverflowPositions(
        IReadOnlyList<SlotPosition> availablePositions,
        int mainCount,
        IReadOnlyList<BoxState> orderedBoxes,
        LivingDexOverflowStart overflowStart)
    {
        if (overflowStart == LivingDexOverflowStart.ImmediatelyAfterEntries || mainCount == 0)
            return availablePositions.Skip(mainCount).ToArray();

        var lastMainBox = availablePositions[mainCount - 1].Box;
        var lastMainOrdinal = Array.FindIndex(
            orderedBoxes.ToArray(),
            box => box.BoxIndex == lastMainBox);
        var allowedBoxes = orderedBoxes
            .Skip(lastMainOrdinal + 1)
            .Select(box => box.BoxIndex)
            .ToHashSet();
        return availablePositions.Where(position => allowedBoxes.Contains(position.Box)).ToArray();
    }

    private static IReadOnlyList<string> CreateWarnings(
        LivingDexOrganizerOptions options,
        int preservedCount)
    {
        var warnings = new List<string>();
        if (options.Mode == LivingDexMode.Shiny)
        {
            warnings.Add(
                "PKHeX does not expose a single reliable shiny-lock definition for every species and form. " +
                "Missing shiny entries are coverage gaps, not a claim that every entry is currently obtainable.");
        }
        if (preservedCount != 0)
        {
            warnings.Add(
                $"{preservedCount} excluded Pokémon remain fixed in their original slots; those slots are never overwritten.");
        }
        return warnings;
    }

    private static LivingDexOrganizationPlan Invalid(
        LivingDexOrganizerOptions options,
        int selectedBoxes,
        int pokemonCount,
        IReadOnlyList<LivingDexEntryDefinition> definitions,
        IReadOnlyList<string> errors,
        IEnumerable<PokemonReference>? preserved = null,
        IEnumerable<MissingLivingDexEntry>? missing = null)
    {
        var missingEntries = missing?.ToArray() ??
            definitions.Select(item => new MissingLivingDexEntry(item)).ToArray();
        return new LivingDexOrganizationPlan(
            options,
            [],
            [],
            preserved ?? [],
            [],
            missingEntries,
            [],
            errors,
            new LivingDexSummary(
                definitions.Count,
                0,
                missingEntries.Length,
                definitions.Count == 0 ? 100 : 0,
                pokemonCount,
                0,
                0,
                0,
                preserved?.Count() ?? 0,
                0,
                0,
                0,
                selectedBoxes,
                selectedBoxes * BoxCapacity,
                0));
    }

    private sealed class RepresentativeComparer(
        LivingDexRepresentativePreference preference) : IComparer<LivingDexCandidate>
    {
        public int Compare(LivingDexCandidate? left, LivingDexCandidate? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return 1;
            if (right is null)
                return -1;

            var comparison = Descending(left.IsLegal, right.IsLegal);
            if (comparison != 0)
                return comparison;

            comparison = preference switch
            {
                LivingDexRepresentativePreference.DefaultSafest => CompareDefault(left, right),
                LivingDexRepresentativePreference.OldestObtained => CompareOldest(left, right),
                LivingDexRepresentativePreference.Strongest => CompareStrongest(left, right),
                _ => throw new ArgumentOutOfRangeException(),
            };
            if (comparison != 0)
                return comparison;
            comparison = left.Reference.SourceBoxIndex.CompareTo(right.Reference.SourceBoxIndex);
            if (comparison != 0)
                return comparison;
            comparison = left.Reference.SourceSlotIndex.CompareTo(right.Reference.SourceSlotIndex);
            if (comparison != 0)
                return comparison;
            return StringComparer.Ordinal.Compare(left.Reference.StableId, right.Reference.StableId);
        }

        private static int CompareDefault(LivingDexCandidate left, LivingDexCandidate right)
        {
            var comparison = Descending(left.IsOwnedByCurrentTrainer, right.IsOwnedByCurrentTrainer);
            if (comparison != 0)
                return comparison;
            comparison = Descending(!left.IsEgg, !right.IsEgg);
            if (comparison != 0)
                return comparison;
            comparison = Descending(left.IsFavoriteOrProtected, right.IsFavoriteOrProtected);
            if (comparison != 0)
                return comparison;
            comparison = Descending(left.Level, right.Level);
            if (comparison != 0)
                return comparison;
            comparison = Descending(left.IvTotal, right.IvTotal);
            if (comparison != 0)
                return comparison;
            return Descending(left.RibbonOrMarkCount, right.RibbonOrMarkCount);
        }

        private static int CompareOldest(LivingDexCandidate left, LivingDexCandidate right)
        {
            var comparison = Descending(left.ObtainedDate is not null, right.ObtainedDate is not null);
            if (comparison != 0)
                return comparison;
            if (left.ObtainedDate is { } leftDate && right.ObtainedDate is { } rightDate)
                return leftDate.CompareTo(rightDate);
            return 0;
        }

        private static int CompareStrongest(LivingDexCandidate left, LivingDexCandidate right)
        {
            var comparison = Descending(left.Level, right.Level);
            if (comparison != 0)
                return comparison;
            comparison = Descending(left.IvTotal, right.IvTotal);
            if (comparison != 0)
                return comparison;
            return Descending(left.EvTotal, right.EvTotal);
        }

        private static int Descending<T>(T left, T right) where T : IComparable<T> =>
            right.CompareTo(left);
    }
}
