using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class SmartTeamBuilderPlannerTests
{
    private readonly SmartTeamBuilderPlanner planner = new();

    [Fact]
    public void HighestLevelThenExperienceSelectsLexicographically()
    {
        var plan = Plan([Box("low", 1, level: 99, exp: 9999), Box("high", 2, level: 100, exp: 1)], size: 1);
        Assert.Equal("high", plan.SelectedTeam[0].StableId);
    }

    [Fact]
    public void CurrentTeamWinsFinalTieRegardlessOfInputOrder()
    {
        var party = Team("team", 0, 1, 50);
        var boxed = Box("box", 2, level: 50);
        Assert.Equal("team", Plan([boxed, party], size: 1).SelectedTeam[0].StableId);
        Assert.Equal("team", Plan([party, boxed], size: 1).SelectedTeam[0].StableId);
    }

    [Theory]
    [InlineData(TeamTypeMatchingMode.HasAnySelectedType, true)]
    [InlineData(TeamTypeMatchingMode.HasAllSelectedTypes, true)]
    [InlineData(TeamTypeMatchingMode.ExactTypeCombination, true)]
    public void DualTypeMatchesBothTypesInEitherOrder(TeamTypeMatchingMode mode, bool expected)
    {
        var candidate = Box("dual", 1, primary: PokemonElementType.Electric, secondary: PokemonElementType.Water);
        Assert.Equal(expected, SmartTeamBuilderPlanner.MatchesTypes(candidate,
            [PokemonElementType.Water, PokemonElementType.Electric], mode));
    }

    [Fact]
    public void PureTypeFailsAllAndExactDualType()
    {
        var candidate = Box("pure", 1, primary: PokemonElementType.Water);
        Assert.False(SmartTeamBuilderPlanner.MatchesTypes(candidate,
            [PokemonElementType.Water, PokemonElementType.Electric], TeamTypeMatchingMode.HasAllSelectedTypes));
        Assert.False(SmartTeamBuilderPlanner.MatchesTypes(candidate,
            [PokemonElementType.Water, PokemonElementType.Electric], TeamTypeMatchingMode.ExactTypeCombination));
        Assert.True(SmartTeamBuilderPlanner.MatchesTypes(candidate,
            [PokemonElementType.Water], TeamTypeMatchingMode.ExactTypeCombination));
    }

    [Fact]
    public void EnabledEligibilityRulesCombineWithAnd()
    {
        var rules = new TeamEligibilityRule[]
        {
            new(TeamEligibilityRuleType.RequiredTypes, true, [PokemonElementType.Water]),
            new(TeamEligibilityRuleType.RequiredOriginGame, true, OriginGame: 10),
            new(TeamEligibilityRuleType.ShinyOnly, true),
        };
        var candidates = new[]
        {
            Box("match", 1, origin: 10, shiny: true, primary: PokemonElementType.Water),
            Box("wrong-origin", 2, origin: 11, shiny: true, primary: PokemonElementType.Water),
            Box("not-shiny", 3, origin: 10, primary: PokemonElementType.Water),
        };
        var plan = Plan(candidates, size: 1, rules: rules);
        Assert.Equal("match", plan.SelectedTeam.Single().StableId);
    }

    [Fact]
    public void GenerationEligibilityIsIndependentOfOrigin()
    {
        var rules = new[] { new TeamEligibilityRule(TeamEligibilityRuleType.RequiredSpeciesGeneration, true, SpeciesGeneration: 4) };
        var plan = Plan([Box("lucario", 448, origin: 1, generation: 4), Box("pikachu", 25, origin: 4, generation: 1)], 1, rules);
        Assert.Equal("lucario", plan.SelectedTeam.Single().StableId);
    }

    [Fact]
    public void LegendaryAndShinyEligibilityExcludeOrdinaryCandidates()
    {
        var rules = new[]
        {
            new TeamEligibilityRule(TeamEligibilityRuleType.LegendaryOrMythicalOnly, true),
            new TeamEligibilityRule(TeamEligibilityRuleType.ShinyOnly, true),
        };
        var plan = Plan([
            Box("legend", 150, legendary: true, shiny: true),
            Box("ordinary", 1, shiny: true),
            Box("not-shiny", 151, legendary: true)], 1, rules);
        Assert.Equal("legend", plan.SelectedTeam.Single().StableId);
    }

    [Fact]
    public void EggsAndInvalidCandidatesAreExcluded()
    {
        var plan = Plan([Box("egg", 1, egg: true), Box("invalid", 2, valid: false), Box("ok", 3)], 1);
        Assert.Equal("ok", plan.SelectedTeam.Single().StableId);
        Assert.Equal(1, plan.Summary.ExcludedEggs);
        Assert.Equal(1, plan.Summary.ExcludedInvalid);
    }

    [Fact]
    public void InvalidCurrentTeamRejectsPlan()
    {
        var plan = Plan([Team("bad", 0, 1, valid: false)], 1);
        Assert.False(plan.IsValid);
        Assert.Contains(plan.Errors, x => x.Contains("invalid Pokémon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChangingPreferenceOrderChangesWinner()
    {
        var level = new TeamPreferenceCriterion(TeamPreferenceCriterionType.HighestLevelAndExperience, true);
        var shiny = new TeamPreferenceCriterion(TeamPreferenceCriterionType.PreferShiny, true);
        var candidates = new[] { Box("level", 1, level: 100), Box("shiny", 2, level: 80, shiny: true) };
        Assert.Equal("level", Plan(candidates, 1, preferences: [level, shiny]).SelectedTeam.Single().StableId);
        Assert.Equal("shiny", Plan(candidates, 1, preferences: [shiny, level]).SelectedTeam.Single().StableId);
    }

    [Fact]
    public void PreferredTypeCoverageRanksTwoThenOneThenNone()
    {
        var preference = new TeamPreferenceCriterion(TeamPreferenceCriterionType.PreferredTypes, true,
            [PokemonElementType.Water, PokemonElementType.Electric]);
        var plan = Plan([
            Box("none", 1, primary: PokemonElementType.Fire),
            Box("one", 2, primary: PokemonElementType.Water),
            Box("two", 3, primary: PokemonElementType.Water, secondary: PokemonElementType.Electric)], 3,
            preferences: [preference], diverse: false);
        Assert.Equal(["two", "one", "none"], plan.SelectedTeam.Select(x => x.StableId));
    }

    [Fact]
    public void DifferentSpeciesUsesTwoPassSelection()
    {
        var candidates = new[] { Box("a1", 1, level: 100), Box("a2", 1, slot: 1, level: 99), Box("b", 2, slot: 2, level: 50) };
        Assert.Equal(["a1", "b"], Plan(candidates, 2, diverse: true).SelectedTeam.Select(x => x.StableId));
        Assert.Equal(["a1", "a2"], Plan(candidates, 2, diverse: false).SelectedTeam.Select(x => x.StableId));
    }

    [Fact]
    public void VacatedBoxSlotReceivesDisplacedTeamPokemon()
    {
        var plan = Plan([Team("old", 0, 1, 10), Box("new", 2, level: 100)], 1);
        Assert.True(plan.IsValid);
        Assert.Contains(plan.FinalBoxAssignments, x => x.StableId == "old" && x.BoxIndex == 0 && x.SlotIndex == 0);
        Assert.Contains(plan.LocationChanges, x => x.StableId == "new" && x.Destination.IsParty);
    }

    [Fact]
    public void EmptyBoxSlotStoresTeamPokemonWhenTeamShrinks()
    {
        var plan = Plan([Team("keep", 0, 1, 100), Team("store", 1, 2, 10)], 1,
            empty: [new SlotPosition(0, 5)]);
        Assert.True(plan.IsValid);
        Assert.Contains(plan.FinalBoxAssignments, x => x.StableId == "store" && x.SlotIndex == 5);
    }

    [Fact]
    public void InsufficientStorageRejectsWithoutPartialPlan()
    {
        var plan = Plan([Team("keep", 0, 1, 100), Team("store", 1, 2, 10)], 1);
        Assert.False(plan.IsValid);
        Assert.Empty(plan.SelectedTeam);
        Assert.Contains(plan.Errors, x => x.Contains("usable storage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreserveTeamOrderDoesNotChangeSelectedMembership()
    {
        var candidates = new[] { Team("first", 0, 1, 50), Team("second", 1, 2, 90), Box("box", 3, level: 100) };
        var preference = Plan(candidates, 2, order: TeamPartyOrder.PreferenceOrder);
        var preserved = Plan(candidates, 2, order: TeamPartyOrder.PreserveCurrentTeamOrder);
        Assert.Equal(preference.SelectedTeam.Select(x => x.StableId).Order(), preserved.SelectedTeam.Select(x => x.StableId).Order());
        Assert.Equal(["second", "box"], preserved.SelectedTeam.Select(x => x.StableId));
    }

    [Fact]
    public void EveryAffectedIdentityAppearsExactlyOnce()
    {
        var candidates = new[] { Team("old", 0, 1, 10), Box("new", 2, level: 100), Box("stay", 3, slot: 1, level: 20) };
        var plan = Plan(candidates, 1);
        var final = plan.SelectedTeam.Select(x => x.StableId).Concat(plan.FinalBoxAssignments.Select(x => x.StableId)).ToArray();
        Assert.Equal(candidates.Length, final.Length);
        Assert.Equal(candidates.Length, final.Distinct(StringComparer.Ordinal).Count());
        var repeated = Plan(candidates.Reverse().ToArray(), 1);
        Assert.Equal(plan.SelectedTeam.Select(x => (x.StableId, x.FinalTeamSlot)),
            repeated.SelectedTeam.Select(x => (x.StableId, x.FinalTeamSlot)));
        Assert.Equal(plan.FinalBoxAssignments, repeated.FinalBoxAssignments);
        Assert.Equal(plan.LocationChanges, repeated.LocationChanges);
    }

    [Fact]
    public void InvalidRuleConfigurationIsRejected()
    {
        var duplicateTypes = new[] { PokemonElementType.Water, PokemonElementType.Water };
        var rules = new[] { new TeamEligibilityRule(TeamEligibilityRuleType.RequiredTypes, true, duplicateTypes) };
        Assert.False(Plan([Box("x", 1)], 1, rules).IsValid);
        var badGeneration = new[] { new TeamEligibilityRule(TeamEligibilityRuleType.RequiredSpeciesGeneration, true, SpeciesGeneration: 10) };
        Assert.False(Plan([Box("x", 1)], 1, badGeneration).IsValid);
    }

    [Fact]
    public void SmallerTeamRequiresExplicitPermission()
    {
        Assert.False(Plan([Box("one", 1)], 2).IsValid);
        Assert.True(Plan([Box("one", 1)], 2, allowSmaller: true).IsValid);
    }

    private TeamExchangePlan Plan(
        IReadOnlyList<TeamBuilderCandidate> candidates,
        int size = 6,
        IReadOnlyList<TeamEligibilityRule>? rules = null,
        IReadOnlyList<TeamPreferenceCriterion>? preferences = null,
        bool diverse = true,
        TeamPartyOrder order = TeamPartyOrder.PreferenceOrder,
        IReadOnlyList<SlotPosition>? empty = null,
        bool allowSmaller = false)
    {
        preferences ??= [new(TeamPreferenceCriterionType.HighestLevelAndExperience, true)];
        var options = new TeamBuilderOptions(size, 6, rules ?? [], preferences, diverse, order, new HashSet<int> { 0 }, false, allowSmaller);
        return planner.CreatePlan(candidates, empty ?? [], options);
    }

    private static TeamBuilderCandidate Team(string id, int slot, int species, int level = 50, bool valid = true) =>
        Candidate(id, PokemonStorageLocation.Party(slot), species, level: level, valid: valid);

    private static TeamBuilderCandidate Box(string id, int species, int slot = 0, int level = 50, ulong exp = 100,
        PokemonElementType primary = PokemonElementType.Normal, PokemonElementType? secondary = null,
        int origin = 1, int generation = 1, bool legendary = false, bool shiny = false, bool egg = false, bool valid = true) =>
        Candidate(id, PokemonStorageLocation.BoxSlot(0, slot), species, level, exp, primary, secondary, origin, generation, legendary, shiny, egg, valid);

    private static TeamBuilderCandidate Candidate(string id, PokemonStorageLocation location, int species, int level = 50,
        ulong exp = 100, PokemonElementType primary = PokemonElementType.Normal, PokemonElementType? secondary = null,
        int origin = 1, int generation = 1, bool legendary = false, bool shiny = false, bool egg = false, bool valid = true) =>
        new(id, location, species, 0, id, level, exp, primary, secondary, origin, $"Game {origin}", generation,
            legendary, shiny, egg, valid);
}
