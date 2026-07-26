using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class CompetitiveOrganizationPlannerTests
{
    private readonly CompetitiveOrganizationPlanner planner = new();

    [Fact]
    public void EmptyInputProducesValidEmptyPlan() => Assert.True(Plan([]).IsValid);

    [Fact]
    public void ProgressClassificationUsesPrecedenceAndRequirements()
    {
        GroupedPokemon[] pokemon =
        [
            Pokemon(0, 50, ev: 508, legal: true, moves: true),
            Pokemon(1, 80, ev: 100),
            Pokemon(2, 20),
            Pokemon(3, 19),
            Pokemon(4, 1, egg: true),
            Pokemon(5, 100, valid: false),
        ];

        var plan = Plan(pokemon, new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ProgressGroups));

        Assert.Equal(
            ["Battle Ready", "High Level", "In Training", "Low Level", "Eggs", "Invalid"],
            plan.GroupCounts.Select(group => group.DisplayName));
    }

    [Fact]
    public void InsufficientEvsPreventsBattleReady() =>
        Assert.Equal("High Level", Assert.Single(Plan([Pokemon(0, 50, ev: 507)]).GroupCounts).DisplayName);

    [Fact]
    public void OptionalLegalAndMoveRequirementsAreApplied()
    {
        var options = new CompetitiveOrganizerOptions(
            CompetitiveOrganizationMode.ProgressGroups,
            requireLegal: true,
            requireAllMoves: true);

        Assert.Equal("High Level", Assert.Single(Plan([Pokemon(0, 50, ev: 508, legal: false, moves: true)], options).GroupCounts).DisplayName);
        Assert.Equal("High Level", Assert.Single(Plan([Pokemon(0, 50, ev: 508, legal: true, moves: false)], options).GroupCounts).DisplayName);
        Assert.Equal("Battle Ready", Assert.Single(Plan([Pokemon(0, 50, ev: 508, legal: true, moves: true)], options).GroupCounts).DisplayName);
    }

    [Theory]
    [InlineData(1, "Lv 1-19")]
    [InlineData(19, "Lv 1-19")]
    [InlineData(20, "Lv 20-49")]
    [InlineData(49, "Lv 20-49")]
    [InlineData(50, "Lv 50-79")]
    [InlineData(79, "Lv 50-79")]
    [InlineData(80, "Lv 80-100")]
    [InlineData(100, "Lv 80-100")]
    public void LevelBandBoundariesAreInclusive(int level, string expected)
    {
        var plan = Plan([Pokemon(0, level)], new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.LevelBands));
        Assert.Equal(expected, Assert.Single(plan.GroupCounts).DisplayName);
    }

    [Fact]
    public void InvalidThresholdOrderingIsRejected()
    {
        var options = new CompetitiveOrganizerOptions(
            CompetitiveOrganizationMode.LevelBands,
            highLevelThreshold: 20,
            trainingThreshold: 20);
        Assert.False(Plan([Pokemon(0, 20)], options).IsValid);
    }

    [Fact]
    public void ExperienceOrderIsDeterministicAndPutsEggsLast()
    {
        var pokemon = new[]
        {
            Pokemon(0, 30, exp: 100), Pokemon(1, 20, exp: 200), Pokemon(2, 99, exp: 200), Pokemon(3, 1, egg: true),
        };
        var first = Plan(pokemon, new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ExperienceOrder));
        var second = Plan(pokemon.Reverse().ToArray(), new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ExperienceOrder));

        Assert.Equal(["P002", "P001", "P000", "P003"], first.Assignments.Select(item => item.Pokemon.StableId));
        Assert.Equal(first.Assignments, second.Assignments);
    }

    [Fact]
    public void GroupBoundariesCanRequireMoreBoxesThanRawCapacity()
    {
        var pokemon = Enumerable.Range(0, 11).Select(id => Pokemon(id, 50, ev: 508))
            .Concat(Enumerable.Range(11, 11).Select(id => Pokemon(id, 50)))
            .Concat(Enumerable.Range(22, 11).Select(id => Pokemon(id, 25))).ToArray();
        var plan = Plan(pokemon, new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ProgressGroups), boxes: 2);
        Assert.False(plan.IsValid);
        Assert.Contains("requires 3 boxes", Assert.Single(plan.Errors));
    }

    [Fact]
    public void RenamingAndBackgroundsAreIndependent()
    {
        var options = new CompetitiveOrganizerOptions(
            CompetitiveOrganizationMode.ProgressGroups,
            renameBoxes: true,
            assignMatchingBackgrounds: true);
        var plan = Plan([Pokemon(0, 50, ev: 508)], options);

        Assert.Equal("Battle Ready", Assert.Single(plan.RenameOperations).NewName);
        Assert.Equal(BoxBackgroundTheme.Metal, Assert.Single(plan.Boxes).BackgroundTheme);
    }

    [Fact]
    public void CustomThresholdsAndMaximumNameLengthAreRespected()
    {
        var options = new CompetitiveOrganizerOptions(
            CompetitiveOrganizationMode.LevelBands,
            highLevelThreshold: 60,
            trainingThreshold: 10,
            endgameThreshold: 90,
            renameBoxes: true,
            maximumBoxNameLength: 7);
        var plan = Plan([Pokemon(0, 59)], options);

        Assert.Equal("Lv 10-59", Assert.Single(plan.GroupCounts).DisplayName);
        Assert.True(Assert.Single(plan.RenameOperations).NewName.Length <= 7);
    }

    [Fact]
    public void EveryPokemonAppearsOnceAtUniqueDestination()
    {
        var pokemon = Enumerable.Range(0, 75).Select(id => Pokemon(id, (id % 100) + 1)).ToArray();
        var plan = Plan(pokemon, new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ExperienceOrder), 3);
        Assert.Equal(75, plan.Assignments.Select(item => item.Pokemon).Distinct().Count());
        Assert.Equal(75, plan.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());
    }

    private GroupedOrganizationPlan Plan(
        IReadOnlyList<GroupedPokemon> pokemon,
        CompetitiveOrganizerOptions? options = null,
        int boxes = 6) => planner.CreatePlan(
            pokemon,
            Enumerable.Range(0, boxes).Select(index => new BoxState(index, $"Box {index + 1}")).ToArray(),
            options ?? new CompetitiveOrganizerOptions(CompetitiveOrganizationMode.ProgressGroups));

    internal static GroupedPokemon Pokemon(
        int id,
        int level,
        uint exp = 0,
        int ev = 0,
        bool egg = false,
        bool valid = true,
        bool legal = true,
        bool moves = true,
        int species = -1,
        bool shiny = false,
        int origin = 1,
        int gender = 0,
        PokemonElementType? type = PokemonElementType.Normal) => new(
            new PokemonReference($"P{id:D3}", id / 30, id % 30),
            species < 0 ? id + 1 : species,
            0,
            $"Species {species}",
            level,
            exp,
            ev,
            shiny,
            origin,
            $"Game {origin}",
            gender,
            type,
            type?.ToString() ?? "Unknown Type",
            egg,
            valid,
            legal,
            moves);
}
