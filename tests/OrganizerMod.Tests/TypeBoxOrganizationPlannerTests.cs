using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class TypeBoxOrganizationPlannerTests
{
    private readonly TypeBoxOrganizationPlanner planner = new();

    [Fact]
    public void EmptyInputProducesEmptyValidPlan()
    {
        var plan = Plan([], 1);

        Assert.True(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Empty(plan.Boxes);
    }

    [Fact]
    public void OneSingleTypePokemonIsAssignedToItsType()
    {
        var plan = Plan([Pokemon(0, PokemonElementType.Water)], 1);

        var box = Assert.Single(plan.Boxes);
        Assert.Equal(PokemonElementType.Water, box.SharedType);
        Assert.False(box.IsMixed);
    }

    [Theory]
    [InlineData(30, 1, 0)]
    [InlineData(31, 1, 1)]
    [InlineData(60, 2, 0)]
    public void OneTypeCreatesExpectedFullAndPartialBoxes(
        int count,
        int expectedFull,
        int expectedPartial)
    {
        var pokemon = Many(0, count, PokemonElementType.Water);
        var plan = Plan(
            pokemon,
            Math.Max(2, (count + 29) / 30),
            TypeBoxLayoutMode.ExpandedByType);

        Assert.Equal(expectedFull, plan.Summary.FullTypeBoxes);
        Assert.Equal(expectedPartial, plan.Summary.PartialTypeBoxes);
        Assert.All(plan.Boxes, box => Assert.Equal(PokemonElementType.Water, box.SharedType));
    }

    [Fact]
    public void DualTypePokemonCompletesTwentyNineMemberGroup()
    {
        var pokemon = Many(0, 29, PokemonElementType.Water)
            .Concat(Many(29, 18, PokemonElementType.Grass))
            .Append(Pokemon(47, PokemonElementType.Water, PokemonElementType.Grass))
            .ToArray();

        var plan = Plan(pokemon, 2);
        var dual = plan.Assignments.Single(item => item.Pokemon.StableId == "P047");

        Assert.Equal(PokemonElementType.Water, dual.AssignedType);
        Assert.Equal(1, plan.Summary.FullTypeBoxes);
    }

    [Fact]
    public void JointAssignmentProducesMoreFullBoxesThanPrimaryOnly()
    {
        var pokemon = Many(0, 28, PokemonElementType.Water)
            .Concat(Many(28, 28, PokemonElementType.Grass))
            .Concat(
            [
                Pokemon(56, PokemonElementType.Fire, PokemonElementType.Water),
                Pokemon(57, PokemonElementType.Fire, PokemonElementType.Water),
                Pokemon(58, PokemonElementType.Fire, PokemonElementType.Grass),
                Pokemon(59, PokemonElementType.Fire, PokemonElementType.Grass),
            ])
            .ToArray();

        var plan = Plan(pokemon, 2);

        Assert.Equal(2, plan.Summary.FullTypeBoxes);
        Assert.All(
            plan.Assignments.Where(item => item.Pokemon.StableId is "P056" or "P057"),
            item => Assert.Equal(PokemonElementType.Water, item.AssignedType));
        Assert.All(
            plan.Assignments.Where(item => item.Pokemon.StableId is "P058" or "P059"),
            item => Assert.Equal(PokemonElementType.Grass, item.AssignedType));
    }

    [Fact]
    public void AssignmentCanImproveNonPrimarySeed()
    {
        var pokemon = Many(0, 29, PokemonElementType.Fire)
            .Append(Pokemon(29, PokemonElementType.Water, PokemonElementType.Fire))
            .ToArray();

        var plan = Plan(pokemon, 1);

        Assert.Equal(PokemonElementType.Fire, plan.Assignments.Single(item => item.Pokemon.StableId == "P029").AssignedType);
        Assert.Equal(1, plan.Summary.FullTypeBoxes);
    }

    [Fact]
    public void LocalImprovementReassignsLargeAmbiguousSet()
    {
        var pokemon = Many(0, 29, PokemonElementType.Water)
            .Concat(Enumerable.Range(29, 17)
                .Select(id => Pokemon(id, PokemonElementType.Fire, PokemonElementType.Water)))
            .ToArray();

        var plan = Plan(pokemon, 2);

        Assert.Equal(1, plan.Summary.FullTypeBoxes);
        Assert.Contains(
            plan.Assignments.Where(item => item.Pokemon.StableId.CompareTo("P029") >= 0),
            item => item.AssignedType == PokemonElementType.Water);
    }

    [Fact]
    public void TieBreakingIsDeterministic()
    {
        var pokemon = Enumerable.Range(0, 12)
            .Select(index => Pokemon(index, PokemonElementType.Water, PokemonElementType.Grass))
            .ToArray();

        var first = Plan(pokemon, 1);
        var second = Plan(pokemon, 1);

        Assert.Equal(first.Assignments, second.Assignments);
        Assert.Equal(
            first.Boxes.Select(box => (box.TargetBoxIndex, box.SharedType, box.IsMixed, string.Join(",", box.Pokemon.Select(item => item.StableId)))),
            second.Boxes.Select(box => (box.TargetBoxIndex, box.SharedType, box.IsMixed, string.Join(",", box.Pokemon.Select(item => item.StableId)))));
    }

    [Fact]
    public void CompactCreatesFullBoxesBeforeMixedOverflow()
    {
        var pokemon = Many(0, 30, PokemonElementType.Water)
            .Concat(Many(30, 8, PokemonElementType.Fire))
            .Concat(Many(38, 8, PokemonElementType.Grass))
            .ToArray();

        var plan = Plan(pokemon, 2);

        Assert.Equal(1, plan.Summary.FullTypeBoxes);
        Assert.Equal(0, plan.Summary.PartialTypeBoxes);
        Assert.Equal(1, plan.Summary.MixedBoxes);
        Assert.Equal(2, plan.Summary.UsedBoxes);
        Assert.Equal(16, plan.Boxes.Single(box => box.IsMixed).Pokemon.Count);
    }

    [Fact]
    public void CompactDoesNotCreateOneNearlyEmptyBoxPerType()
    {
        var pokemon = Many(0, 10, PokemonElementType.Water)
            .Concat(Many(10, 10, PokemonElementType.Fire))
            .Concat(Many(20, 10, PokemonElementType.Grass))
            .ToArray();

        var plan = Plan(pokemon, 3);

        var mixed = Assert.Single(plan.Boxes);
        Assert.True(mixed.IsMixed);
        Assert.Equal(30, mixed.Pokemon.Count);
        Assert.Equal(1, plan.Summary.UsedBoxes);
    }

    [Fact]
    public void ExpandedCreatesSeparatePartialBoxes()
    {
        var pokemon = Many(0, 10, PokemonElementType.Water)
            .Concat(Many(10, 10, PokemonElementType.Fire))
            .Concat(Many(20, 10, PokemonElementType.Grass))
            .ToArray();

        var plan = Plan(pokemon, 3, TypeBoxLayoutMode.ExpandedByType);

        Assert.Equal(3, plan.Summary.PartialTypeBoxes);
        Assert.Equal(0, plan.Summary.MixedBoxes);
        Assert.All(plan.Boxes, box => Assert.False(box.IsMixed));
    }

    [Fact]
    public void ExpandedKeepsSameTypeBoxesAdjacent()
    {
        var pokemon = Many(0, 61, PokemonElementType.Water)
            .Concat(Many(61, 12, PokemonElementType.Fire))
            .ToArray();

        var plan = Plan(pokemon, 4, TypeBoxLayoutMode.ExpandedByType);
        var waterIndices = plan.Boxes
            .Select((box, index) => (box, index))
            .Where(pair => pair.box.SharedType == PokemonElementType.Water)
            .Select(pair => pair.index)
            .ToArray();

        Assert.Equal(3, waterIndices.Length);
        Assert.Equal(waterIndices[0] + 1, waterIndices[1]);
        Assert.Equal(waterIndices[1] + 1, waterIndices[2]);
    }

    [Fact]
    public void ExpandedWarnsAndUsesBestEffortMixedOverflowWhenBoxesAreLimited()
    {
        var pokemon = Many(0, 10, PokemonElementType.Water)
            .Concat(Many(10, 10, PokemonElementType.Fire))
            .Concat(Many(20, 10, PokemonElementType.Grass))
            .ToArray();

        var plan = Plan(pokemon, 1, TypeBoxLayoutMode.ExpandedByType);

        Assert.True(plan.IsValid);
        Assert.NotEmpty(plan.Warnings);
        Assert.Equal(1, plan.Summary.MixedBoxes);
    }

    [Fact]
    public void InsufficientCapacityReturnsInvalidNonApplicablePlan()
    {
        var plan = Plan(Many(0, 31, PokemonElementType.Water), 1);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Contains("Insufficient capacity", Assert.Single(plan.Errors));
    }

    [Fact]
    public void ExcludedBoxesAreNeitherSourcesNorDestinations()
    {
        var pokemon = Many(0, 20, PokemonElementType.Water, sourceBoxes: [0, 2]);
        var boxes = new[] { new BoxState(0, "A"), new BoxState(2, "C") };
        var plan = planner.CreatePlan(pokemon, boxes, Options(TypeBoxLayoutMode.Compact));

        Assert.True(plan.IsValid);
        Assert.DoesNotContain(plan.Assignments, item => item.TargetBoxIndex == 1);
        Assert.DoesNotContain(plan.Assignments, item => item.Pokemon.SourceBoxIndex == 1);
    }

    [Fact]
    public void PokemonFromUnselectedBoxIsRejectedRatherThanMoved()
    {
        var pokemon = new[] { Pokemon(0, PokemonElementType.Water, sourceBox: 1) };
        var plan = planner.CreatePlan(
            pokemon,
            [new BoxState(0, "Selected")],
            Options(TypeBoxLayoutMode.Compact));

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void RenamingDisabledPreservesNamesAndCreatesNoOperations()
    {
        var plan = Plan(
            Many(0, 30, PokemonElementType.Water),
            1,
            rename: false,
            names: ["Storage"]);

        Assert.Empty(plan.RenameOperations);
    }

    [Fact]
    public void RenamingNamesSingleTypeBox()
    {
        var plan = Plan(
            Many(0, 30, PokemonElementType.Water),
            1,
            rename: true,
            names: ["Storage"]);

        var rename = Assert.Single(plan.RenameOperations);
        Assert.Equal("Storage", rename.OriginalName);
        Assert.Equal("Water", rename.NewName);
    }

    [Fact]
    public void MultipleTypeAndMixedBoxesAreNumbered()
    {
        var pokemon = Many(0, 61, PokemonElementType.Water)
            .Concat(Many(61, 40, PokemonElementType.Fire))
            .Concat(Many(101, 20, PokemonElementType.Grass))
            .ToArray();
        var plan = Plan(pokemon, 5, rename: true, names: ["A", "B", "C", "D", "E"]);

        Assert.Contains(plan.RenameOperations, item => item.NewName == "Water 1");
        Assert.Contains(plan.RenameOperations, item => item.NewName == "Water 2");
        Assert.DoesNotContain(plan.RenameOperations, item => item.BoxIndex >= plan.Summary.UsedBoxes);
    }

    [Fact]
    public void TypeBoxesFollowStandardOrderAndSameTypeBoxesStayAdjacent()
    {
        PokemonElementType[] standardOrder =
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
        var pokemon = new List<OrganizablePokemon>();
        var nextId = 0;
        foreach (var type in standardOrder)
        {
            var count = type == PokemonElementType.Water ? 31 : 1;
            pokemon.AddRange(Many(nextId, count, type));
            nextId += count;
        }

        var plan = Plan(pokemon.ToArray(), 19, TypeBoxLayoutMode.ExpandedByType);
        var expected = standardOrder
            .SelectMany(type => type == PokemonElementType.Water ? new[] { type, type } : new[] { type })
            .ToArray();

        Assert.True(plan.IsValid);
        Assert.Equal(expected, plan.Boxes.Select(box => box.SharedType!.Value));
    }

    [Fact]
    public void MultipleMixedBoxesAreNumbered()
    {
        var pokemon = Enum.GetValues<PokemonElementType>()
            .Take(7)
            .SelectMany((type, typeIndex) => Many(typeIndex * 8, 8, type))
            .ToArray();
        var plan = Plan(pokemon, 2, rename: true, names: ["A", "B"]);

        Assert.Equal(2, plan.Summary.MixedBoxes);
        Assert.Equal(["Mixed 1", "Mixed 2"], plan.RenameOperations.Select(item => item.NewName));
    }

    [Fact]
    public void GeneratedNamesRespectMaximumLengthDeterministically()
    {
        var pokemon = Many(0, 31, PokemonElementType.Electric);
        var boxes = new[] { new BoxState(0, "A"), new BoxState(1, "B") };
        var options = new TypeBoxOrganizerOptions(
            TypeBoxLayoutMode.ExpandedByType,
            true,
            6,
            new Dictionary<PokemonElementType, string>
            {
                [PokemonElementType.Electric] = "Electric",
            });

        var first = planner.CreatePlan(pokemon, boxes, options);
        var second = planner.CreatePlan(pokemon, boxes, options);

        Assert.All(first.RenameOperations, item => Assert.True(item.NewName.Length <= 6));
        Assert.Equal(["Elec 1", "Elec 2"], first.RenameOperations.Select(item => item.NewName));
        Assert.Equal(first.RenameOperations, second.RenameOperations);
    }

    [Fact]
    public void CompleteTargetLayoutPreservesEveryPokemonExactlyOnce()
    {
        var pokemon = Many(0, 75, PokemonElementType.Water, PokemonElementType.Flying);
        var plan = Plan(pokemon, 3);

        Assert.Equal(pokemon.Length, plan.Assignments.Count);
        Assert.Equal(
            pokemon.Select(item => item.Reference.StableId).Order(),
            plan.Assignments.Select(item => item.Pokemon.StableId).Order());
        Assert.Equal(
            plan.Assignments.Count,
            plan.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());
        Assert.All(plan.Boxes, box => Assert.True(box.Pokemon.Count <= 30));
    }

    [Fact]
    public void TypeBoxesAreCoherentAndRandomizedInvariantsHold()
    {
        var random = new Random(24051996);
        var types = Enum.GetValues<PokemonElementType>();
        var pokemon = Enumerable.Range(0, 420)
            .Select(index =>
            {
                var primary = types[random.Next(types.Length)];
                var secondary = random.Next(3) == 0
                    ? types[random.Next(types.Length)]
                    : primary;
                return Pokemon(index, primary, secondary, sourceBox: index / 30, sourceSlot: index % 30);
            })
            .ToArray();

        foreach (var mode in Enum.GetValues<TypeBoxLayoutMode>())
        {
            var plan = Plan(pokemon, 14, mode);
            Assert.True(plan.IsValid);
            Assert.Equal(pokemon.Length, plan.Assignments.Count);
            Assert.Equal(
                plan.Assignments.Count,
                plan.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());

            var pokemonByReference = pokemon.ToDictionary(item => item.Reference);
            foreach (var box in plan.Boxes.Where(box => !box.IsMixed))
            {
                Assert.All(
                    box.Pokemon,
                    reference => Assert.True(pokemonByReference[reference].CanBeAssignedTo(box.SharedType!.Value)));
            }
        }
    }

    [Fact]
    public void LegendaryGroupingDisabledLeavesPokemonInTypeGroups()
    {
        var pokemon = new[] { Pokemon(0, PokemonElementType.Psychic, legendary: true) };

        var plan = Plan(pokemon, 1, groupLegendaries: false);

        var box = Assert.Single(plan.Boxes);
        Assert.False(box.IsLegendary);
        Assert.Equal(PokemonElementType.Psychic, box.SharedType);
        Assert.Equal(0, plan.Summary.LegendaryPokemon);
    }

    [Fact]
    public void LegendaryGroupingCombinesDifferentTypesInDedicatedBox()
    {
        var pokemon = new[]
        {
            Pokemon(0, PokemonElementType.Psychic, legendary: true),
            Pokemon(1, PokemonElementType.Water, legendary: true),
            Pokemon(2, PokemonElementType.Fire),
        };

        var plan = Plan(pokemon, 2, groupLegendaries: true);

        Assert.True(plan.IsValid);
        var legendary = Assert.Single(plan.Boxes, box => box.IsLegendary);
        Assert.Null(legendary.SharedType);
        Assert.False(legendary.IsMixed);
        Assert.Equal(2, legendary.Pokemon.Count);
        Assert.All(
            plan.Assignments.Where(item => item.IsLegendary),
            item => Assert.Null(item.AssignedType));
        Assert.Equal(1, plan.Summary.LegendaryBoxes);
        Assert.Equal(2, plan.Summary.LegendaryPokemon);
    }

    [Fact]
    public void ThirtyOneLegendariesCreateTwoAdjacentDedicatedBoxes()
    {
        var pokemon = Enumerable.Range(0, 31)
            .Select(id => Pokemon(id, PokemonElementType.Psychic, legendary: true))
            .ToArray();

        var plan = Plan(pokemon, 2, groupLegendaries: true);

        Assert.Equal(2, plan.Summary.LegendaryBoxes);
        Assert.Equal([30, 1], plan.Boxes.Select(box => box.Pokemon.Count));
        Assert.All(plan.Boxes, box => Assert.True(box.IsLegendary));
    }

    [Fact]
    public void DedicatedLegendaryBoxCapacityIsValidatedBeforePlanning()
    {
        var pokemon = new[] { Pokemon(0, PokemonElementType.Psychic, legendary: true) }
            .Concat(Many(1, 31, PokemonElementType.Water))
            .ToArray();

        var plan = Plan(pokemon, 2, groupLegendaries: true);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
        Assert.Contains("Grouping 1 Legendary Pokémon separately", Assert.Single(plan.Errors));
    }

    [Fact]
    public void LegendaryBoxesAreNamedAndNumberedWhenRenamingIsEnabled()
    {
        var pokemon = Enumerable.Range(0, 31)
            .Select(id => Pokemon(id, PokemonElementType.Psychic, legendary: true))
            .ToArray();

        var plan = Plan(
            pokemon,
            2,
            rename: true,
            names: ["A", "B"],
            groupLegendaries: true);

        Assert.Equal(["Legendary 1", "Legendary 2"], plan.RenameOperations.Select(item => item.NewName));
    }

    [Fact]
    public void LegendaryGroupingIntegratesWithRenamingAndBackgroundPlanning()
    {
        var pokemon = new[]
        {
            Pokemon(0, PokemonElementType.Psychic, legendary: true),
            Pokemon(1, PokemonElementType.Water),
        };
        var boxes = new[] { new BoxState(0, "A"), new BoxState(1, "B") };
        var options = new TypeBoxOrganizerOptions(
            TypeBoxLayoutMode.ExpandedByType,
            renameBoxes: true,
            assignMatchingBackgrounds: true,
            rotateAlternativeBackgrounds: true,
            supportedBackgroundThemes: Enum.GetValues<BoxBackgroundTheme>().ToHashSet(),
            groupLegendaries: true);

        var plan = planner.CreatePlan(pokemon, boxes, options);

        Assert.True(plan.IsValid);
        Assert.Equal("Legendary", plan.RenameOperations.Single(item => item.BoxIndex == 0).NewName);
        Assert.Equal(
            BoxBackgroundTheme.PokemonCenter,
            plan.BackgroundThemes.Single(item => item.BoxIndex == 0).Theme);
        Assert.Equal(2, plan.Assignments.Select(item => item.Pokemon).Distinct().Count());
        Assert.Equal(2, plan.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());
    }

    private TypeOrganizationPlan Plan(
        IReadOnlyList<OrganizablePokemon> pokemon,
        int boxCount,
        TypeBoxLayoutMode mode = TypeBoxLayoutMode.Compact,
        bool rename = false,
        IReadOnlyList<string>? names = null,
        bool groupLegendaries = false)
    {
        var boxes = Enumerable.Range(0, boxCount)
            .Select(index => new BoxState(index, names?[index] ?? $"Box {index + 1}"))
            .ToArray();
        return planner.CreatePlan(pokemon, boxes, Options(mode, rename, groupLegendaries));
    }

    private static TypeBoxOrganizerOptions Options(
        TypeBoxLayoutMode mode,
        bool rename = false,
        bool groupLegendaries = false) =>
        new(mode, rename, groupLegendaries: groupLegendaries);

    private static OrganizablePokemon[] Many(
        int firstId,
        int count,
        PokemonElementType primary,
        PokemonElementType? secondary = null,
        IReadOnlyList<int>? sourceBoxes = null) =>
        Enumerable.Range(firstId, count)
            .Select((id, index) => Pokemon(
                id,
                primary,
                secondary,
                sourceBoxes?[index % sourceBoxes.Count] ?? (id / 30),
                id % 30))
            .ToArray();

    private static OrganizablePokemon Pokemon(
        int id,
        PokemonElementType primary,
        PokemonElementType? secondary = null,
        int sourceBox = -1,
        int sourceSlot = -1,
        bool legendary = false)
    {
        if (sourceBox < 0)
            sourceBox = id / 30;
        if (sourceSlot < 0)
            sourceSlot = id % 30;
        return new OrganizablePokemon(
            new PokemonReference($"P{id:D3}", sourceBox, sourceSlot),
            species: (id % 1025) + 1,
            form: id % 3,
            gender: id % 2,
            isShiny: id % 17 == 0,
            primary,
            secondary,
            isLegendary: legendary);
    }
}
