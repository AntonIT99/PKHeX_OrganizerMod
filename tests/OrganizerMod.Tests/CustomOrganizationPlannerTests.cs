using OrganizerMod.Domain;
using static OrganizerMod.Tests.CompetitiveOrganizationPlannerTests;

namespace OrganizerMod.Tests;

public sealed class CustomOrganizationPlannerTests
{
    private readonly CustomOrganizationPlanner planner = new();

    [Fact]
    public void RejectsLimitsAndDuplicateCriteria()
    {
        var threeGroups = Enum.GetValues<CustomGroupCriterionType>().Take(3)
            .Select((type, index) => new CustomGroupRule(type, true, index)).ToArray();
        Assert.False(Plan([], new CustomOrganizerOptions(threeGroups, [])).IsValid);

        var duplicates = new[] { new CustomSortRule(CustomSortCriterionType.Level, true, OrganizerSortDirection.Ascending, 0), new CustomSortRule(CustomSortCriterionType.Level, true, OrganizerSortDirection.Descending, 1) };
        Assert.False(Plan([], new CustomOrganizerOptions([], duplicates)).IsValid);

        var fiveSorts = Enum.GetValues<CustomSortCriterionType>().Take(5)
            .Select((type, index) => new CustomSortRule(type, true, OrganizerSortDirection.Ascending, index)).ToArray();
        Assert.False(Plan([], new CustomOrganizerOptions([], fiveSorts)).IsValid);
    }

    [Fact]
    public void DisabledRulesHaveNoEffect()
    {
        var options = new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.ShinyStatus, false, 0)],
            [new(CustomSortCriterionType.Level, false, OrganizerSortDirection.Descending, 0)]);
        var plan = Plan([Pokemon(0, 10, shiny: true), Pokemon(1, 100)], options);
        Assert.Single(plan.GroupCounts);
        Assert.Equal(["P000", "P001"], plan.Assignments.Select(item => item.Pokemon.StableId));
    }

    [Theory]
    [InlineData(CustomGroupCriterionType.ShinyStatus, 2)]
    [InlineData(CustomGroupCriterionType.OriginGame, 2)]
    [InlineData(CustomGroupCriterionType.PrimaryType, 2)]
    [InlineData(CustomGroupCriterionType.LevelBand, 2)]
    public void EachGroupingCriterionCreatesStableGroups(CustomGroupCriterionType criterion, int expected)
    {
        var pokemon = new[]
        {
            Pokemon(0, 10, shiny: false, origin: 1, type: PokemonElementType.Water),
            Pokemon(1, 80, shiny: true, origin: 2, type: PokemonElementType.Fire),
        };
        var plan = Plan(pokemon, new CustomOrganizerOptions([new(criterion, true, 0)], []));
        Assert.Equal(expected, plan.GroupCounts.Count);
    }

    [Fact]
    public void TwoRulesCreateHierarchicalOrderAndChangingOrderChangesHierarchy()
    {
        var pokemon = new[]
        {
            Pokemon(0, 20, shiny: true, origin: 2), Pokemon(1, 20, shiny: false, origin: 1),
            Pokemon(2, 20, shiny: true, origin: 1), Pokemon(3, 20, shiny: false, origin: 2),
        };
        var originFirst = Plan(pokemon, new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.OriginGame, true, 0), new(CustomGroupCriterionType.ShinyStatus, true, 1)], []));
        var shinyFirst = Plan(pokemon, new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.OriginGame, true, 1), new(CustomGroupCriterionType.ShinyStatus, true, 0)], []));

        Assert.NotEqual(originFirst.GroupCounts.Select(item => item.GroupId), shinyFirst.GroupCounts.Select(item => item.GroupId));
    }

    [Theory]
    [InlineData(CustomSortCriterionType.NationalDex)]
    [InlineData(CustomSortCriterionType.Level)]
    [InlineData(CustomSortCriterionType.Experience)]
    [InlineData(CustomSortCriterionType.ShinyStatus)]
    [InlineData(CustomSortCriterionType.OriginGame)]
    [InlineData(CustomSortCriterionType.Gender)]
    public void SortingCriteriaSupportBothDirections(CustomSortCriterionType criterion)
    {
        var pokemon = new[]
        {
            Pokemon(0, 10, exp: 10, species: 1, shiny: false, origin: 1, gender: 0),
            Pokemon(1, 90, exp: 90, species: 2, shiny: true, origin: 2, gender: 1),
        };
        var asc = Plan(pokemon, Options(new(criterion, true, OrganizerSortDirection.Ascending, 0)));
        var desc = Plan(pokemon, Options(new(criterion, true, OrganizerSortDirection.Descending, 0)));
        Assert.Equal(asc.Assignments.Select(item => item.Pokemon).Reverse(), desc.Assignments.Select(item => item.Pokemon));
    }

    [Fact]
    public void MultipleSortRulesAreLexicographic()
    {
        var pokemon = new[] { Pokemon(0, 10, species: 2), Pokemon(1, 90, species: 1), Pokemon(2, 100, species: 2) };
        var options = new CustomOrganizerOptions([], [
            new(CustomSortCriterionType.NationalDex, true, OrganizerSortDirection.Ascending, 0),
            new(CustomSortCriterionType.Level, true, OrganizerSortDirection.Descending, 1)]);
        Assert.Equal(["P001", "P002", "P000"], Plan(pokemon, options).Assignments.Select(item => item.Pokemon.StableId));
    }

    [Fact]
    public void NewBoxPerGroupAffectsCapacityWithoutSilentFallback()
    {
        var pokemon = Enumerable.Range(0, 20).Select(id => Pokemon(id, 20, shiny: false))
            .Concat(Enumerable.Range(20, 20).Select(id => Pokemon(id, 20, shiny: true))).ToArray();
        var rule = new CustomGroupRule(CustomGroupCriterionType.ShinyStatus, true, 0);
        Assert.False(Plan(pokemon, new CustomOrganizerOptions([rule], [], StartEachGroupInNewBox: true), 1).IsValid);
        Assert.True(Plan(pokemon, new CustomOrganizerOptions([rule], [], StartEachGroupInNewBox: false), 2).IsValid);
    }

    [Fact]
    public void TypeGroupReusesTypeBackgroundAndRenamingIsIndependent()
    {
        var options = new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.PrimaryType, true, 0)], [],
            RenameBoxes: true, AssignMatchingBackgrounds: true);
        var plan = Plan([Pokemon(0, 10, type: PokemonElementType.Water)], options);
        Assert.Equal("Water", Assert.Single(plan.RenameOperations).NewName);
        Assert.Equal(BoxBackgroundTheme.DeepSea, Assert.Single(plan.Boxes).BackgroundTheme);
    }

    [Fact]
    public void CustomLevelBoundariesDriveGroupsAndInvalidOrderingIsRejected()
    {
        var rule = new CustomGroupRule(CustomGroupCriterionType.LevelBand, true, 0);
        var options = new CustomOrganizerOptions([rule], [], TrainingStart: 10, HighLevelStart: 60, EndgameStart: 90);
        Assert.Equal("Lv 10-59", Assert.Single(Plan([Pokemon(0, 59)], options).GroupCounts).DisplayName);
        Assert.False(Plan([], options with { HighLevelStart = 10 }).IsValid);
    }

    [Fact]
    public void GroupNamesAreDeterministicAndRespectMaximumLength()
    {
        var options = new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.OriginGame, true, 0), new(CustomGroupCriterionType.ShinyStatus, true, 1)],
            [], RenameBoxes: true, MaximumBoxNameLength: 8);
        var plan = Plan([Pokemon(0, 50, shiny: true, origin: 123)], options);
        Assert.True(Assert.Single(plan.RenameOperations).NewName.Length <= 8);
    }

    [Fact]
    public void FixedSeedInvariantsAreDeterministic()
    {
        var random = new Random(7341);
        var pokemon = Enumerable.Range(0, 300).Select(id => Pokemon(
            id, random.Next(1, 101), (uint)random.Next(0, 1_000_000),
            shiny: random.Next(2) == 0, origin: random.Next(1, 8), gender: random.Next(0, 4),
            type: Enum.GetValues<PokemonElementType>()[random.Next(18)])).ToArray();
        var options = new CustomOrganizerOptions(
            [new(CustomGroupCriterionType.PrimaryType, true, 0), new(CustomGroupCriterionType.ShinyStatus, true, 1)],
            [new(CustomSortCriterionType.NationalDex, true, OrganizerSortDirection.Ascending, 0)],
            StartEachGroupInNewBox: false);
        var first = Plan(pokemon, options, 10);
        var second = Plan(pokemon.Reverse().ToArray(), options, 10);
        Assert.Equal(300, first.Assignments.Select(item => item.Pokemon).Distinct().Count());
        Assert.Equal(300, first.Assignments.Select(item => (item.TargetBoxIndex, item.TargetSlotIndex)).Distinct().Count());
        Assert.Equal(first.Assignments, second.Assignments);
    }

    private GroupedOrganizationPlan Plan(IReadOnlyList<GroupedPokemon> pokemon, CustomOrganizerOptions options, int boxes = 10) =>
        planner.CreatePlan(pokemon, Enumerable.Range(0, boxes).Select(index => new BoxState(index, $"Box {index + 1}")).ToArray(), options);

    private static CustomOrganizerOptions Options(CustomSortRule sort) => new([], [sort]);
}
