namespace OrganizerMod.Domain;

public sealed class TypeBoxOrganizationPlanner
{
    public const int BoxCapacity = 30;
    public const int ExhaustiveDualTypeLimit = 16;
    public const int MaximumImprovementIterations = 64;
    public const int MaximumPairEvaluationsPerIteration = 4096;
    private const int CompactPartialMinimumFill = 15;

    // Keep the enum values aligned with PKHeX's stable type identifiers. Box presentation
    // follows the familiar National Pokédex type sequence independently of those values.
    private static readonly PokemonElementType[] BoxTypeOrder =
    [
        PokemonElementType.Normal,
        PokemonElementType.Fire,
        PokemonElementType.Water,
        PokemonElementType.Electric,
        PokemonElementType.Grass,
        PokemonElementType.Ice,
        PokemonElementType.Fighting,
        PokemonElementType.Poison,
        PokemonElementType.Ground,
        PokemonElementType.Flying,
        PokemonElementType.Psychic,
        PokemonElementType.Bug,
        PokemonElementType.Rock,
        PokemonElementType.Ghost,
        PokemonElementType.Dragon,
        PokemonElementType.Dark,
        PokemonElementType.Steel,
        PokemonElementType.Fairy,
    ];

    public TypeOrganizationPlan CreatePlan(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<BoxState> boxes,
        TypeBoxOrganizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);

        var capacity = boxes.Sum(box => box.Capacity);
        if (pokemon.Count > capacity)
        {
            return Invalid(
                options.LayoutMode,
                pokemon.Count,
                boxes.Count,
                [$"Insufficient capacity: {pokemon.Count} Pokémon require {pokemon.Count} slots, but the selected boxes provide only {capacity} slots."]);
        }

        var errors = ValidateInput(pokemon, boxes);
        if (errors.Count != 0)
            return Invalid(options.LayoutMode, pokemon.Count, boxes.Count, errors);

        if (pokemon.Count == 0)
        {
            return new TypeOrganizationPlan(
                options.LayoutMode,
                [],
                [],
                [],
                [],
                [],
                [],
                new TypeOrganizationSummary(0, 0, 0, 0, 0, 0, 0),
                boxes.Count,
                0);
        }

        var orderedPokemon = pokemon.OrderBy(item => item, PokemonComparer.Instance).ToArray();
        var legendaryPokemon = options.GroupLegendaries
            ? orderedPokemon.Where(item => item.IsLegendary).ToArray()
            : [];
        var typedPokemon = options.GroupLegendaries
            ? orderedPokemon.Where(item => !item.IsLegendary).ToArray()
            : orderedPokemon;
        var legendaryBoxCount = DivideRoundUp(legendaryPokemon.Length, BoxCapacity);
        var minimumRequiredBoxes = legendaryBoxCount + DivideRoundUp(typedPokemon.Length, BoxCapacity);
        if (minimumRequiredBoxes > boxes.Count)
        {
            return Invalid(
                options.LayoutMode,
                pokemon.Count,
                boxes.Count,
                [$"Grouping {legendaryPokemon.Length} Legendary Pokémon separately requires at least {minimumRequiredBoxes} boxes, but only {boxes.Count} usable boxes were selected."]);
        }

        var orderedBoxes = boxes.OrderBy(box => box.BoxIndex).ToArray();
        var typedTargetBoxes = orderedBoxes.Skip(legendaryBoxCount).ToArray();
        var targetBoxIndices = typedTargetBoxes.Select(box => box.BoxIndex).ToArray();
        var assignedTypes = OptimizeAssignments(typedPokemon, options.LayoutMode, targetBoxIndices);
        var layout = EvaluateLayout(typedPokemon, assignedTypes, options.LayoutMode, typedTargetBoxes.Length);
        var usedBoxes = legendaryBoxCount + layout.UsedBoxes;
        if (usedBoxes > boxes.Count)
        {
            return Invalid(
                options.LayoutMode,
                pokemon.Count,
                boxes.Count,
                [$"The generated layout requires {usedBoxes} boxes, but only {boxes.Count} usable boxes were selected."]);
        }

        var legendaryGroups = BuildLegendaryGroups(legendaryPokemon, orderedBoxes);
        var typedGroups = BuildGroups(typedPokemon, assignedTypes, layout, typedTargetBoxes);
        var groups = legendaryGroups.Concat(typedGroups).ToArray();
        var assignments = BuildSlotAssignments(groups, typedPokemon, assignedTypes);
        var boxStateByIndex = orderedBoxes.ToDictionary(box => box.BoxIndex);
        var renames = TypeBoxNameGenerator.CreateRenames(groups, boxStateByIndex, options);
        var backgroundThemes = TypeBoxBackgroundPlanner.Create(groups, options);
        var warnings = new List<string>();
        warnings.AddRange(backgroundThemes
            .Where(item => item.Warning is not null)
            .Select(item => item.Warning!));

        if (options.LayoutMode == TypeBoxLayoutMode.ExpandedByType &&
            layout.UnseparatedResidualTypeCount != 0)
        {
            warnings.Add(
                $"Expanded by Type could not keep {layout.UnseparatedResidualTypeCount} represented type group(s) separate with the selected boxes. " +
                "Those residual Pokémon are shown in mixed overflow boxes; choose more usable boxes or Compact mode if this is not acceptable.");
        }

        var summary = new TypeOrganizationSummary(
            layout.FullTypeBoxes,
            layout.PartialTypeBoxes,
            layout.MixedBoxes,
            layout.PokemonInTypeBoxes,
            layout.PokemonInMixedBoxes,
            usedBoxes,
            (usedBoxes * BoxCapacity) - pokemon.Count,
            legendaryBoxCount,
            legendaryPokemon.Length);

        return new TypeOrganizationPlan(
            options.LayoutMode,
            assignments,
            groups,
            renames,
            backgroundThemes,
            warnings,
            [],
            summary,
            boxes.Count,
            pokemon.Count);
    }

    private static IReadOnlyList<string> ValidateInput(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<BoxState> boxes)
    {
        var errors = new List<string>();
        if (boxes.Any(box => box.Capacity != BoxCapacity))
            errors.Add("Type-Optimized Box Allocation currently requires storage boxes with exactly 30 slots.");
        if (boxes.Select(box => box.BoxIndex).Distinct().Count() != boxes.Count)
            errors.Add("The usable box list contains duplicate box indices.");
        if (pokemon.Select(item => item.Reference.StableId).Distinct(StringComparer.Ordinal).Count() != pokemon.Count)
            errors.Add("The Pokémon input contains duplicate stable identities.");
        if (pokemon
                .Select(item => (item.Reference.SourceBoxIndex, item.Reference.SourceSlotIndex))
                .Distinct()
                .Count() != pokemon.Count)
        {
            errors.Add("The Pokémon input contains duplicate source slots.");
        }
        if (pokemon.Any(item => boxes.All(box => box.BoxIndex != item.Reference.SourceBoxIndex)))
            errors.Add("Every eligible Pokémon must originate in one of the selected usable boxes.");
        return errors;
    }

    private static TypeOrganizationPlan Invalid(
        TypeBoxLayoutMode mode,
        int pokemonCount,
        int usableBoxCount,
        IReadOnlyList<string> errors) =>
        new(
            mode,
            [],
            [],
            [],
            [],
            [],
            errors,
            new TypeOrganizationSummary(0, 0, 0, 0, 0, 0, 0),
            usableBoxCount,
            pokemonCount);

    private static PokemonElementType[] OptimizeAssignments(
        IReadOnlyList<OrganizablePokemon> pokemon,
        TypeBoxLayoutMode mode,
        IReadOnlyList<int> targetBoxIndices)
    {
        var result = pokemon.Select(item => item.PrimaryType).ToArray();
        var dualIndices = pokemon
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.SecondaryType is not null)
            .Select(pair => pair.index)
            .ToArray();

        if (dualIndices.Length == 0)
            return result;

        if (dualIndices.Length <= ExhaustiveDualTypeLimit)
            return ExhaustiveAssignment(pokemon, dualIndices, result, mode, targetBoxIndices);

        // Deterministic non-greedy seed: start from the lower stable type identifier,
        // then repeatedly improve the complete assignment.
        foreach (var index in dualIndices)
        {
            var item = pokemon[index];
            result[index] = (PokemonElementType)Math.Min(
                (int)item.PrimaryType,
                (int)item.SecondaryType!.Value);
        }

        var currentScore = Score(pokemon, result, mode, targetBoxIndices);
        for (var iteration = 0; iteration < MaximumImprovementIterations; iteration++)
        {
            PokemonElementType[]? best = null;
            var bestScore = currentScore;

            foreach (var index in dualIndices)
            {
                var candidate = (PokemonElementType[])result.Clone();
                candidate[index] = OtherType(pokemon[index], candidate[index]);
                Consider(candidate, pokemon, mode, targetBoxIndices, ref best, ref bestScore);
            }

            var pairEvaluations = 0;
            for (var left = 0; left < dualIndices.Length && pairEvaluations < MaximumPairEvaluationsPerIteration; left++)
            {
                for (var right = left + 1; right < dualIndices.Length && pairEvaluations < MaximumPairEvaluationsPerIteration; right++)
                {
                    pairEvaluations++;
                    var candidate = (PokemonElementType[])result.Clone();
                    var leftIndex = dualIndices[left];
                    var rightIndex = dualIndices[right];
                    candidate[leftIndex] = OtherType(pokemon[leftIndex], candidate[leftIndex]);
                    candidate[rightIndex] = OtherType(pokemon[rightIndex], candidate[rightIndex]);
                    Consider(candidate, pokemon, mode, targetBoxIndices, ref best, ref bestScore);
                }
            }

            if (best is null)
                break;
            result = best;
            currentScore = bestScore;
        }

        return result;
    }

    private static PokemonElementType[] ExhaustiveAssignment(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<int> dualIndices,
        PokemonElementType[] seed,
        TypeBoxLayoutMode mode,
        IReadOnlyList<int> targetBoxIndices)
    {
        PokemonElementType[]? best = null;
        AllocationScore? bestScore = null;
        var stateCount = 1 << dualIndices.Count;
        for (var state = 0; state < stateCount; state++)
        {
            var candidate = (PokemonElementType[])seed.Clone();
            for (var bit = 0; bit < dualIndices.Count; bit++)
            {
                var index = dualIndices[bit];
                var item = pokemon[index];
                candidate[index] = (state & (1 << bit)) == 0
                    ? item.PrimaryType
                    : item.SecondaryType!.Value;
            }

            var score = Score(pokemon, candidate, mode, targetBoxIndices);
            if (bestScore is null ||
                score.CompareTo(bestScore.Value) > 0 ||
                (score == bestScore && IsLexicographicallyEarlier(candidate, best!)))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best!;
    }

    private static void Consider(
        PokemonElementType[] candidate,
        IReadOnlyList<OrganizablePokemon> pokemon,
        TypeBoxLayoutMode mode,
        IReadOnlyList<int> targetBoxIndices,
        ref PokemonElementType[]? best,
        ref AllocationScore bestScore)
    {
        var score = Score(pokemon, candidate, mode, targetBoxIndices);
        if (score.CompareTo(bestScore) <= 0)
            return;
        if (best is null ||
            score.CompareTo(bestScore) > 0 ||
            IsLexicographicallyEarlier(candidate, best))
        {
            best = candidate;
            bestScore = score;
        }
    }

    private static bool IsLexicographicallyEarlier(
        IReadOnlyList<PokemonElementType> candidate,
        IReadOnlyList<PokemonElementType> current)
    {
        for (var index = 0; index < candidate.Count; index++)
        {
            var comparison = ((int)candidate[index]).CompareTo((int)current[index]);
            if (comparison != 0)
                return comparison < 0;
        }

        return false;
    }

    private static PokemonElementType OtherType(
        OrganizablePokemon pokemon,
        PokemonElementType current) =>
        current == pokemon.PrimaryType ? pokemon.SecondaryType!.Value : pokemon.PrimaryType;

    private static AllocationScore Score(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<PokemonElementType> assignments,
        TypeBoxLayoutMode mode,
        IReadOnlyList<int> targetBoxIndices)
    {
        var layout = EvaluateLayout(pokemon, assignments, mode, targetBoxIndices.Count);
        var moveCount = EstimateMoveCount(pokemon, assignments, layout, targetBoxIndices);

        return mode switch
        {
            TypeBoxLayoutMode.Compact => new AllocationScore(
                layout.FullTypeBoxes,
                layout.PokemonInTypeBoxes,
                -layout.PokemonInMixedBoxes,
                -layout.UsedBoxes,
                -((layout.UsedBoxes * BoxCapacity) - pokemon.Count),
                -moveCount),
            TypeBoxLayoutMode.ExpandedByType => new AllocationScore(
                layout.RepresentedTypes,
                layout.PokemonInTypeBoxes,
                -layout.PokemonInMixedBoxes,
                layout.FullTypeBoxes,
                0,
                -moveCount),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static LayoutDecision EvaluateLayout(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<PokemonElementType> assignments,
        TypeBoxLayoutMode mode,
        int availableBoxes)
    {
        var counts = new int[Enum.GetValues<PokemonElementType>().Length];
        foreach (var type in assignments)
            counts[(int)type]++;

        var fullBoxes = counts.Sum(count => count / BoxCapacity);
        var residuals = Enum.GetValues<PokemonElementType>()
            .Select(type => new TypeResidual(type, counts[(int)type] % BoxCapacity, counts[(int)type]))
            .Where(item => item.Count != 0)
            .ToArray();

        var kept = mode switch
        {
            TypeBoxLayoutMode.Compact => ChooseCompactResiduals(residuals),
            TypeBoxLayoutMode.ExpandedByType => ChooseExpandedResiduals(
                residuals,
                fullBoxes,
                availableBoxes),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        var keptSet = kept.Select(item => item.Type).ToHashSet();
        var coherentResidualPokemon = kept.Sum(item => item.Count);
        var totalResidualPokemon = residuals.Sum(item => item.Count);
        var mixedPokemon = totalResidualPokemon - coherentResidualPokemon;
        var mixedBoxes = DivideRoundUp(mixedPokemon, BoxCapacity);
        var partialBoxes = kept.Count;
        var usedBoxes = fullBoxes + partialBoxes + mixedBoxes;
        var representedTypes = Enum.GetValues<PokemonElementType>()
            .Count(type =>
                counts[(int)type] >= BoxCapacity ||
                keptSet.Contains(type));
        var unseparated = residuals.Count(item => !keptSet.Contains(item.Type));

        return new LayoutDecision(
            keptSet,
            fullBoxes,
            partialBoxes,
            mixedBoxes,
            (fullBoxes * BoxCapacity) + coherentResidualPokemon,
            mixedPokemon,
            usedBoxes,
            representedTypes,
            unseparated);
    }

    private static IReadOnlyList<TypeResidual> ChooseCompactResiduals(
        IReadOnlyList<TypeResidual> residuals)
    {
        if (residuals.Count == 1)
            return residuals;

        var remaining = residuals.Sum(item => item.Count);
        var chosen = new List<TypeResidual>();
        foreach (var candidate in residuals
                     .Where(item => item.Count >= CompactPartialMinimumFill)
                     .OrderByDescending(item => item.Count)
                     .ThenBy(item => item.Type))
        {
            var currentBoxes = chosen.Count + DivideRoundUp(remaining, BoxCapacity);
            var proposedBoxes = chosen.Count + 1 + DivideRoundUp(remaining - candidate.Count, BoxCapacity);
            if (proposedBoxes > currentBoxes)
                continue;

            chosen.Add(candidate);
            remaining -= candidate.Count;
        }

        return chosen;
    }

    private static IReadOnlyList<TypeResidual> ChooseExpandedResiduals(
        IReadOnlyList<TypeResidual> residuals,
        int fullBoxes,
        int availableBoxes)
    {
        if (fullBoxes + residuals.Count <= availableBoxes)
            return residuals.OrderBy(item => item.Type).ToArray();

        var remainingBoxCount = availableBoxes - fullBoxes;
        var remainingPokemon = residuals.Sum(item => item.Count);
        var candidates = residuals.ToList();
        var chosen = new List<TypeResidual>();

        while (candidates.Count != 0)
        {
            TypeResidual? best = null;
            (int NewRepresentation, int BoxCost, int NegativeCount, int Type) bestRank =
                (int.MinValue, int.MaxValue, int.MaxValue, int.MaxValue);

            foreach (var candidate in candidates)
            {
                var proposedUsed = chosen.Count + 1 +
                                   DivideRoundUp(remainingPokemon - candidate.Count, BoxCapacity);
                if (proposedUsed > remainingBoxCount)
                    continue;

                var currentUsed = chosen.Count + DivideRoundUp(remainingPokemon, BoxCapacity);
                var rank = (
                    candidate.TotalForType < BoxCapacity ? 1 : 0,
                    proposedUsed - currentUsed,
                    -candidate.Count,
                    (int)candidate.Type);
                if (best is null || CompareExpandedCandidate(rank, bestRank) > 0)
                {
                    best = candidate;
                    bestRank = rank;
                }
            }

            if (best is null)
                break;
            chosen.Add(best);
            candidates.Remove(best);
            remainingPokemon -= best.Count;
        }

        return chosen.OrderBy(item => item.Type).ToArray();
    }

    private static int CompareExpandedCandidate(
        (int NewRepresentation, int BoxCost, int NegativeCount, int Type) left,
        (int NewRepresentation, int BoxCost, int NegativeCount, int Type) right)
    {
        var comparison = left.NewRepresentation.CompareTo(right.NewRepresentation);
        if (comparison != 0)
            return comparison;
        comparison = right.BoxCost.CompareTo(left.BoxCost);
        if (comparison != 0)
            return comparison;
        comparison = right.NegativeCount.CompareTo(left.NegativeCount);
        if (comparison != 0)
            return comparison;
        return right.Type.CompareTo(left.Type);
    }

    private static IReadOnlyList<TypeBoxAssignment> BuildGroups(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<PokemonElementType> assignments,
        LayoutDecision layout,
        IReadOnlyList<BoxState> targetBoxes)
    {
        var byType = Enum.GetValues<PokemonElementType>()
            .ToDictionary(type => type, _ => new List<OrganizablePokemon>());
        for (var index = 0; index < pokemon.Count; index++)
            byType[assignments[index]].Add(pokemon[index]);

        var logicalGroups = new List<(PokemonElementType? Type, bool Mixed, OrganizablePokemon[] Pokemon)>();
        var mixed = new List<OrganizablePokemon>();
        foreach (var type in BoxTypeOrder)
        {
            var ordered = byType[type].OrderBy(item => item, PokemonComparer.Instance).ToArray();
            var fullBoxCount = ordered.Length / BoxCapacity;
            for (var box = 0; box < fullBoxCount; box++)
            {
                logicalGroups.Add((
                    type,
                    false,
                    ordered.Skip(box * BoxCapacity).Take(BoxCapacity).ToArray()));
            }

            var residual = ordered.Skip(fullBoxCount * BoxCapacity).ToArray();
            if (residual.Length == 0)
                continue;
            if (layout.KeptResidualTypes.Contains(type))
                logicalGroups.Add((type, false, residual));
            else
                mixed.AddRange(residual);
        }

        var orderedMixed = mixed.OrderBy(item => item, PokemonComparer.Instance).ToArray();
        for (var offset = 0; offset < orderedMixed.Length; offset += BoxCapacity)
        {
            logicalGroups.Add((
                null,
                true,
                orderedMixed.Skip(offset).Take(BoxCapacity).ToArray()));
        }

        // Type groups are generated in stable enum order and are therefore adjacent.
        // Mixed overflow always follows the coherent groups.
        return logicalGroups
            .Select((group, index) => new TypeBoxAssignment(
                targetBoxes[index].BoxIndex,
                group.Type,
                group.Pokemon.Select(item => item.Reference),
                group.Mixed))
            .ToArray();
    }

    private static IReadOnlyList<TypeBoxAssignment> BuildLegendaryGroups(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<BoxState> targetBoxes)
    {
        var result = new List<TypeBoxAssignment>(DivideRoundUp(pokemon.Count, BoxCapacity));
        for (var offset = 0; offset < pokemon.Count; offset += BoxCapacity)
        {
            var group = pokemon.Skip(offset).Take(BoxCapacity).ToArray();
            result.Add(new TypeBoxAssignment(
                targetBoxes[offset / BoxCapacity].BoxIndex,
                null,
                group.Select(item => item.Reference),
                isMixed: false,
                isLegendary: true));
        }

        return result;
    }

    private static IReadOnlyList<TypeSlotAssignment> BuildSlotAssignments(
        IReadOnlyList<TypeBoxAssignment> boxes,
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<PokemonElementType> assignedTypes)
    {
        var typeByReference = pokemon
            .Select((item, index) => (item.Reference, Type: assignedTypes[index]))
            .ToDictionary(pair => pair.Reference, pair => pair.Type);
        var result = new List<TypeSlotAssignment>(pokemon.Count);
        foreach (var box in boxes)
        {
            for (var slot = 0; slot < box.Pokemon.Count; slot++)
            {
                var reference = box.Pokemon[slot];
                var isLegendary = box.IsLegendary;
                result.Add(new TypeSlotAssignment(
                    reference,
                    box.TargetBoxIndex,
                    slot,
                    isLegendary ? null : typeByReference[reference],
                    box.IsMixed,
                    isLegendary));
            }
        }

        return result;
    }

    private static int EstimateMoveCount(
        IReadOnlyList<OrganizablePokemon> pokemon,
        IReadOnlyList<PokemonElementType> assignments,
        LayoutDecision layout,
        IReadOnlyList<int> targetBoxIndices)
    {
        var moveCount = 0;
        var targetGroup = 0;
        var mixedIndices = new List<int>();
        var indicesByType = new List<int>[Enum.GetValues<PokemonElementType>().Length];
        for (var index = 0; index < pokemon.Count; index++)
        {
            var typeIndex = (int)assignments[index];
            (indicesByType[typeIndex] ??= []).Add(index);
        }

        foreach (var type in Enum.GetValues<PokemonElementType>())
        {
            var indices = indicesByType[(int)type] ?? [];
            var coherentCount = (indices.Count / BoxCapacity) * BoxCapacity;
            if (layout.KeptResidualTypes.Contains(type))
                coherentCount = indices.Count;

            for (var position = 0; position < coherentCount; position++)
            {
                var item = pokemon[indices[position]];
                var box = targetBoxIndices[targetGroup + (position / BoxCapacity)];
                var slot = position % BoxCapacity;
                if (item.Reference.SourceBoxIndex != box ||
                    item.Reference.SourceSlotIndex != slot)
                {
                    moveCount++;
                }
            }

            targetGroup += DivideRoundUp(coherentCount, BoxCapacity);
            for (var index = coherentCount; index < indices.Count; index++)
                mixedIndices.Add(indices[index]);
        }

        mixedIndices.Sort((left, right) => PokemonComparer.Instance.Compare(pokemon[left], pokemon[right]));
        for (var position = 0; position < mixedIndices.Count; position++)
        {
            var item = pokemon[mixedIndices[position]];
            var box = targetBoxIndices[targetGroup + (position / BoxCapacity)];
            var slot = position % BoxCapacity;
            if (item.Reference.SourceBoxIndex != box ||
                item.Reference.SourceSlotIndex != slot)
            {
                moveCount++;
            }
        }

        return moveCount;
    }

    private static int DivideRoundUp(int value, int divisor) =>
        value == 0 ? 0 : ((value - 1) / divisor) + 1;

    private sealed class PokemonComparer : IComparer<OrganizablePokemon>
    {
        public static PokemonComparer Instance { get; } = new();

        public int Compare(OrganizablePokemon? left, OrganizablePokemon? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            var comparison = left.Species.CompareTo(right.Species);
            if (comparison != 0)
                return comparison;
            comparison = left.Form.CompareTo(right.Form);
            if (comparison != 0)
                return comparison;
            comparison = left.Gender.CompareTo(right.Gender);
            if (comparison != 0)
                return comparison;
            comparison = left.IsShiny.CompareTo(right.IsShiny);
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
    }

    private sealed record TypeResidual(
        PokemonElementType Type,
        int Count,
        int TotalForType);

    private sealed record LayoutDecision(
        IReadOnlySet<PokemonElementType> KeptResidualTypes,
        int FullTypeBoxes,
        int PartialTypeBoxes,
        int MixedBoxes,
        int PokemonInTypeBoxes,
        int PokemonInMixedBoxes,
        int UsedBoxes,
        int RepresentedTypes,
        int UnseparatedResidualTypeCount);
}
